#include <cstring>
#include <harp_c_app.h>
#include <harp_synchronizer.h>
#include <core_registers.h>
#include <reg_types.h>
#include <hardware/gpio.h>
#include <hardware/adc.h>
#include <hardware/dma.h>
#include <hardware/pwm.h>
#include <hardware/clocks.h>
#include <pico/util/queue.h>
#include <config.h>

// Harp App state.
bool events_active = false;

// Addresses of the app registers used in the code, as declared in device.yml.
inline constexpr uint8_t DIGITAL_INPUT_STATE_ADDRESS =
    HarpCore::APP_REG_START_ADDRESS + 0;
inline constexpr uint8_t DIGITAL_OUTPUT_SET_ADDRESS =
    HarpCore::APP_REG_START_ADDRESS + 1;
inline constexpr uint8_t ANALOG_DATA_ADDRESS =
    HarpCore::APP_REG_START_ADDRESS + 5;
inline constexpr uint8_t PULSE_TRAIN_GP15_ADDRESS =
    HarpCore::APP_REG_START_ADDRESS + 6;
inline constexpr uint8_t PULSE_EVENT_ENABLE_ADDRESS =
    HarpCore::APP_REG_START_ADDRESS + 14;

// Pulse trains, one per digital output. GP18, GP20 and GP22 are channel A of PWM
// slices 1, 2 and 3, so each can run its train in hardware at its own frequency.
constexpr size_t pulse_train_count = 8;
constexpr uint8_t hardware_pulse_mask = 0xA8;
constexpr uint32_t min_pulse_period_us = 1000;
constexpr uint32_t min_hardware_period_us = 10;
constexpr uint32_t max_pulse_event_rate_hz = 2000;
struct pulse_train_t
{
    alarm_id_t alarm;
    uint pin;
    uint slice;
    uint8_t output_mask;
    uint8_t generation;
    bool running;
    bool hardware;
    bool high;
    bool last_pulse;
    uint32_t width_us;
    uint32_t period_us;
    uint32_t remaining;
    uint64_t last_rise_us;
};
pulse_train_t pulse_trains[pulse_train_count];

// Repeating timer and buffers for ADC sampling using
// Pointer to an address is required for the reinitialization DMA channel.
uint16_t adc_vals[3] = {0, 0, 0};
uint16_t* data_ptr[1] = {adc_vals};
struct repeating_timer adc_timer;
const int32_t adc_period_us = 4000;
const int32_t adc_callback_delay_us = 80000;
int adc_sample_channel;
int adc_ctrl_channel;
const uint adc_queue_size = 2;
static queue_t adc_queue;

// Queue for events raised in interrupt callbacks.
const uint event_queue_size = 64;
static queue_t event_queue;

// Define queue item contents
#pragma pack(push, 1)
struct adc_queue_item_t
{
    uint64_t timestamp;
    uint16_t analog_data[3];
};

struct event_queue_item_t
{
    uint64_t timestamp;
    uint8_t address;
    uint8_t value;
};
#pragma pack(pop)

// Define register contents.
struct app_regs_t
{
    volatile uint8_t di_state;
    volatile uint8_t do_set;
    volatile uint8_t do_clear;
    volatile uint8_t do_toggle;
    volatile uint8_t do_state;
    volatile uint16_t analog_data[3];
    volatile uint32_t pulse_train[pulse_train_count][3];
    volatile uint8_t pulse_event_enable;
} app_regs;

// Never blocks, so it is safe to call from interrupts; drops the event if full.
void queue_event(uint8_t address, uint8_t value, uint64_t harp_time_us)
{
    event_queue_item_t item{harp_time_us, address, value};
    queue_try_add(&event_queue, &item);
}

uint8_t read_digital_inputs()
{
    uint32_t gpio_state = gpio_get_all();
    return ((gpio_state & 0xC) >> 2) | ((gpio_state & 0x7000) >> 10);
}

void gpio_callback(uint gpio, uint32_t events)
{
    // Capture the GPIO state and timestamp.
    queue_event(DIGITAL_INPUT_STATE_ADDRESS, read_digital_inputs(),
                HarpCore::harp_time_us_64());
}

void read_di_state(uint8_t address)
{
    app_regs.di_state = read_digital_inputs();
    HarpCore::read_reg_generic(address);
}

// One alarm per train alternates rising and falling edges. A negative return
// reschedules relative to the previous target time, so the period never drifts.
int64_t pulse_train_callback(alarm_id_t id, void *user_data)
{
    pulse_train_t *pulse_train = (pulse_train_t *)user_data;
    if (!pulse_train->high)
    {
        gpio_set_mask(pulse_train->output_mask << DO0_PIN);
        pulse_train->high = true;
        if (app_regs.pulse_event_enable & pulse_train->output_mask)
        {
            queue_event(DIGITAL_OUTPUT_SET_ADDRESS, pulse_train->output_mask,
                        HarpCore::harp_time_us_64());
        }

        // A count of zero runs until stopped.
        if (pulse_train->remaining > 0)
            pulse_train->last_pulse = --pulse_train->remaining == 0;

        // Stay high when the width fills the period, except after the last pulse.
        if (pulse_train->width_us < pulse_train->period_us || pulse_train->last_pulse)
            return -(int64_t)pulse_train->width_us;
        pulse_train->high = false;
        return -(int64_t)pulse_train->period_us;
    }

    gpio_clr_mask(pulse_train->output_mask << DO0_PIN);
    pulse_train->high = false;
    if (pulse_train->last_pulse)
    {
        // Report the end of the train on its register, see send_queued_event.
        pulse_train->alarm = 0;
        pulse_train->running = false;
        uint8_t index = __builtin_ctz(pulse_train->output_mask);
        queue_event(PULSE_TRAIN_GP15_ADDRESS + index, pulse_train->generation,
                    HarpCore::harp_time_us_64());
        return 0;
    }
    return -(int64_t)(pulse_train->period_us - pulse_train->width_us);
}

// The PWM counter is 16 bits, so long periods need a clock divider of up to 255.
// Returns the divider for the period, or zero if it is too long for hardware.
uint32_t pwm_clock_divider(uint32_t period_us)
{
    uint64_t cycles = (uint64_t)period_us * clock_get_hz(clk_sys) / 1000000;
    uint32_t divider = (cycles + 0xFFFE) / 0xFFFF;
    return (divider > 0 && divider <= 255) ? divider : 0;
}

// Wrap interrupts mark each rising edge, needed to report or count pulses.
bool needs_wrap_interrupt(const pulse_train_t *pulse_train)
{
    return pulse_train->remaining > 0 || pulse_train->last_pulse ||
           (app_regs.pulse_event_enable & pulse_train->output_mask);
}

void start_hardware_pulse_train(pulse_train_t *pulse_train)
{
    uint32_t sys_hz = clock_get_hz(clk_sys);
    uint32_t divider = pwm_clock_divider(pulse_train->period_us);
    uint32_t top = (uint64_t)pulse_train->period_us * sys_hz / 1000000 / divider - 1;
    uint32_t level = (uint64_t)pulse_train->width_us * sys_hz / 1000000 / divider;
    if (pulse_train->width_us == pulse_train->period_us)
        level = top + 1;

    // Start at the top, so the first pulse also begins with a wrap interrupt.
    uint slice = pulse_train->slice;
    pwm_set_clkdiv_int_frac4(slice, divider, 0);
    pwm_set_wrap(slice, top);
    pwm_set_chan_level(slice, PWM_CHAN_A, level);
    pwm_set_counter(slice, top);
    pwm_clear_irq(slice);
    pwm_set_irq_enabled(slice, needs_wrap_interrupt(pulse_train));
    gpio_set_function(pulse_train->pin, GPIO_FUNC_PWM);
    pulse_train->hardware = true;
    pwm_set_enabled(slice, true);
}

// Hand the pin back to the GPIO output, low, so writes drive it again.
void stop_hardware_pulse_train(pulse_train_t *pulse_train)
{
    pwm_set_irq_enabled(pulse_train->slice, false);
    pwm_set_enabled(pulse_train->slice, false);
    pwm_clear_irq(pulse_train->slice);
    gpio_clr_mask(pulse_train->output_mask << DO0_PIN);
    gpio_set_function(pulse_train->pin, GPIO_FUNC_SIO);
    pulse_train->hardware = false;
}

// Each wrap is the rising edge of a pulse on a hardware train.
void pwm_wrap_callback()
{
    uint32_t status = pwm_get_irq_status_mask();
    for (size_t i = 0; i < pulse_train_count; i++)
    {
        pulse_train_t *pulse_train = &pulse_trains[i];
        if (!pulse_train->hardware || !(status & (1u << pulse_train->slice)))
            continue;
        pwm_clear_irq(pulse_train->slice);

        // The level set after the last pulse kept the pin low for this period.
        if (pulse_train->last_pulse)
        {
            stop_hardware_pulse_train(pulse_train);
            pulse_train->running = false;
            queue_event(PULSE_TRAIN_GP15_ADDRESS + i, pulse_train->generation,
                        pulse_train->last_rise_us + pulse_train->width_us);
            continue;
        }

        pulse_train->last_rise_us = HarpCore::harp_time_us_64();
        if (app_regs.pulse_event_enable & pulse_train->output_mask)
        {
            queue_event(DIGITAL_OUTPUT_SET_ADDRESS, pulse_train->output_mask,
                        pulse_train->last_rise_us);
        }

        // The channel level is double-buffered, so the current pulse keeps its width.
        if (pulse_train->remaining > 0 && --pulse_train->remaining == 0)
        {
            pwm_set_chan_level(pulse_train->slice, PWM_CHAN_A, 0);
            pulse_train->last_pulse = true;
        }
    }
}

void cancel_pulse_train(pulse_train_t *pulse_train)
{
    if (pulse_train->hardware)
        stop_hardware_pulse_train(pulse_train);
    if (pulse_train->alarm > 0)
        cancel_alarm(pulse_train->alarm);
    pulse_train->alarm = 0;
    pulse_train->running = false;
    pulse_train->high = false;
    pulse_train->last_pulse = false;
}

// Stop the trains on the specified lines, returning the lines that were running.
uint8_t stop_pulse_trains(uint8_t output_mask)
{
    uint8_t stopped = 0;
    for (size_t i = 0; i < pulse_train_count; i++)
    {
        pulse_train_t *pulse_train = &pulse_trains[i];
        if ((output_mask & pulse_train->output_mask) && pulse_train->running)
        {
            cancel_pulse_train(pulse_train);
            app_regs.pulse_train[i][0] = 0;
            stopped |= pulse_train->output_mask;
        }
    }
    return stopped;
}

// Report trains stopped by a write, after the reply to that write.
void send_pulse_train_stop_events(uint8_t stopped)
{
    for (size_t i = 0; i < pulse_train_count; i++)
    {
        if (events_active && (stopped & pulse_trains[i].output_mask))
            HarpCore::send_harp_reply(EVENT, PULSE_TRAIN_GP15_ADDRESS + i);
    }
}

void write_do_set(msg_t &msg)
{
    HarpCore::copy_msg_payload_to_register(msg);
    uint8_t stopped = stop_pulse_trains(app_regs.do_set);
    gpio_set_mask(app_regs.do_set << DO0_PIN);
    HarpCore::send_harp_reply(WRITE, msg.header.address);
    send_pulse_train_stop_events(stopped);
}

void write_do_clear(msg_t &msg)
{
    HarpCore::copy_msg_payload_to_register(msg);
    uint8_t stopped = stop_pulse_trains(app_regs.do_clear);
    gpio_clr_mask(app_regs.do_clear << DO0_PIN);
    HarpCore::send_harp_reply(WRITE, msg.header.address);
    send_pulse_train_stop_events(stopped);
}

void write_do_toggle(msg_t &msg)
{
    HarpCore::copy_msg_payload_to_register(msg);
    uint8_t stopped = stop_pulse_trains(app_regs.do_toggle);
    gpio_xor_mask(app_regs.do_toggle << DO0_PIN);
    HarpCore::send_harp_reply(WRITE, msg.header.address);
    send_pulse_train_stop_events(stopped);
}

void write_do_state(msg_t &msg)
{
    HarpCore::copy_msg_payload_to_register(msg);
    uint8_t stopped = stop_pulse_trains(0xFF);
    gpio_put_masked(DO_MASK, app_regs.do_state << DO0_PIN);
    HarpCore::send_harp_reply(WRITE, msg.header.address);
    send_pulse_train_stop_events(stopped);
}

// Read the pins on request, so the state includes running pulse trains.
void read_do_state(uint8_t address)
{
    app_regs.do_state = (gpio_get_all() & DO_MASK) >> DO0_PIN;
    HarpCore::read_reg_generic(address);
}

// Total rate of per-pulse events from running trains, plus a candidate train.
uint32_t pulse_event_rate_hz(uint8_t event_enable, size_t candidate,
                             uint32_t candidate_period_us)
{
    uint32_t rate_hz = 0;
    for (size_t i = 0; i < pulse_train_count; i++)
    {
        uint32_t period_us = 0;
        if (i == candidate)
            period_us = candidate_period_us;
        else if (pulse_trains[i].running)
            period_us = pulse_trains[i].period_us;
        if (period_us > 0 && (event_enable & pulse_trains[i].output_mask))
            rate_hz += 1000000 / period_us;
    }
    return rate_hz;
}

void write_pulse_train(msg_t &msg)
{
    // Validate before storing, so a rejected write leaves the register unchanged.
    size_t index = msg.header.address - PULSE_TRAIN_GP15_ADDRESS;
    uint32_t payload[3];
    if (msg.header.payload_length() != sizeof(payload))
    {
        HarpCore::write_reg_error(msg);
        return;
    }
    memcpy(payload, msg.payload, sizeof(payload)); // payload may be unaligned
    uint32_t width_us = payload[0];
    uint32_t period_us = payload[1];
    uint32_t count = payload[2];

    // Only hardware trains without wrap interrupts may run faster than software.
    pulse_train_t *pulse_train = &pulse_trains[index];
    bool hardware = (hardware_pulse_mask & pulse_train->output_mask) &&
                    pwm_clock_divider(period_us) > 0;
    bool interrupts = !hardware || count > 0 ||
                      (app_regs.pulse_event_enable & pulse_train->output_mask);
    uint32_t min_period_us = interrupts ? min_pulse_period_us : min_hardware_period_us;
    if (width_us > 0 && (period_us < min_period_us || width_us > period_us ||
        pulse_event_rate_hz(app_regs.pulse_event_enable, index, period_us) >
        max_pulse_event_rate_hz))
    {
        HarpCore::write_reg_error(msg);
        return;
    }

    // Each write supersedes any end of train still queued, see send_queued_event.
    cancel_pulse_train(pulse_train);
    gpio_clr_mask(pulse_train->output_mask << DO0_PIN);
    pulse_train->generation++;
    for (size_t i = 0; i < 3; i++)
        app_regs.pulse_train[index][i] = payload[i];

    // A width of zero stops the train, keeping the period and count.
    if (width_us > 0)
    {
        pulse_train->width_us = width_us;
        pulse_train->period_us = period_us;
        pulse_train->remaining = count;
        pulse_train->running = true;
        if (hardware)
            start_hardware_pulse_train(pulse_train);
        else
            pulse_train->alarm = add_alarm_in_us(0, pulse_train_callback, pulse_train, true);
    }
    HarpCore::send_harp_reply(WRITE, msg.header.address);
}

void write_pulse_event_enable(msg_t &msg)
{
    uint8_t event_enable = *(uint8_t *)msg.payload;
    if (pulse_event_rate_hz(event_enable, pulse_train_count, 0) > max_pulse_event_rate_hz)
    {
        HarpCore::write_reg_error(msg);
        return;
    }
    HarpCore::copy_msg_payload_to_register(msg);

    // Running hardware trains only take wrap interrupts while they need them.
    for (size_t i = 0; i < pulse_train_count; i++)
    {
        if (pulse_trains[i].hardware)
            pwm_set_irq_enabled(pulse_trains[i].slice, needs_wrap_interrupt(&pulse_trains[i]));
    }
    HarpCore::send_harp_reply(WRITE, msg.header.address);
}

bool adc_callback(repeating_timer_t *rt)
{
    if (!HarpCore::events_enabled())
        return false;

    rt->delay_us = -adc_period_us;

    // Mask the values to 12 bits (0xFFF) to ensure only valid ADC bits are used
    adc_queue_item_t item;
    item.timestamp = HarpCore::harp_time_us_64();
    item.analog_data[0] = adc_vals[0] & 0xFFF;
    item.analog_data[1] = adc_vals[1] & 0xFFF;
    item.analog_data[2] = adc_vals[2] & 0xFFF;

    // Never block in a timer callback: drop the samples if the queue is full.
    queue_try_add(&adc_queue, &item);
    return true;
}

// Define register specs with their read-and-write handler functions.
RegSpec app_reg_specs[]
{
    RegSpec::U8(&app_regs.di_state,
        read_di_state, HarpCore::write_reg_error),
    RegSpec::U8(&app_regs.do_set,
        HarpCore::read_reg_generic, write_do_set),
    RegSpec::U8(&app_regs.do_clear,
        HarpCore::read_reg_generic, write_do_clear),
    RegSpec::U8(&app_regs.do_toggle,
        HarpCore::read_reg_generic, write_do_toggle),
    RegSpec::U8(&app_regs.do_state,
        read_do_state, write_do_state),
    RegSpec::U16Array(&app_regs.analog_data, 3,
        HarpCore::read_reg_generic, HarpCore::write_reg_error),
    RegSpec::U32Array(&app_regs.pulse_train[0], 3,
        HarpCore::read_reg_generic, write_pulse_train),
    RegSpec::U32Array(&app_regs.pulse_train[1], 3,
        HarpCore::read_reg_generic, write_pulse_train),
    RegSpec::U32Array(&app_regs.pulse_train[2], 3,
        HarpCore::read_reg_generic, write_pulse_train),
    RegSpec::U32Array(&app_regs.pulse_train[3], 3,
        HarpCore::read_reg_generic, write_pulse_train),
    RegSpec::U32Array(&app_regs.pulse_train[4], 3,
        HarpCore::read_reg_generic, write_pulse_train),
    RegSpec::U32Array(&app_regs.pulse_train[5], 3,
        HarpCore::read_reg_generic, write_pulse_train),
    RegSpec::U32Array(&app_regs.pulse_train[6], 3,
        HarpCore::read_reg_generic, write_pulse_train),
    RegSpec::U32Array(&app_regs.pulse_train[7], 3,
        HarpCore::read_reg_generic, write_pulse_train),
    RegSpec::U8(&app_regs.pulse_event_enable,
        HarpCore::read_reg_generic, write_pulse_event_enable)
};

const size_t app_reg_count = sizeof(app_reg_specs) / sizeof(RegSpec);

// Stop every pulse train and return the outputs and their registers to defaults.
void reset_outputs()
{
    for (size_t i = 0; i < pulse_train_count; i++)
    {
        cancel_pulse_train(&pulse_trains[i]);
        pulse_trains[i].output_mask = 1 << i;
        pulse_trains[i].pin = DO0_PIN + i;
        pulse_trains[i].slice = pwm_gpio_to_slice_num(DO0_PIN + i);
        for (size_t j = 0; j < 3; j++)
            app_regs.pulse_train[i][j] = 0;
    }
    gpio_clr_mask(DO_MASK);
    app_regs.pulse_event_enable = 0xFF;
}

void app_reset()
{
    app_regs.di_state = 0;
    app_regs.do_set = 0;
    app_regs.do_clear = 0;
    app_regs.do_toggle = 0;
    app_regs.do_state = 0;
    app_regs.analog_data[0] = 0;
    app_regs.analog_data[1] = 0;
    app_regs.analog_data[2] = 0;
    reset_outputs();
}

void configure_gpio(void)
{
    gpio_init_mask(DO_MASK | DI_MASK);
    gpio_set_dir_out_masked(DO_MASK);
    gpio_set_dir_in_masked(DI_MASK);
    gpio_clr_mask(DO_MASK);

    // Configure queue for interrupt events and avoid concurrency in outbound
    // message buffers, i.e. avoid sending reply in GPIO and timer callbacks.
    queue_init(&event_queue, sizeof(event_queue_item_t), event_queue_size);
    gpio_set_irq_callback(gpio_callback);
    gpio_set_irq_enabled(2, GPIO_IRQ_EDGE_FALL | GPIO_IRQ_EDGE_RISE, true);
    gpio_set_irq_enabled(3, GPIO_IRQ_EDGE_FALL | GPIO_IRQ_EDGE_RISE, true);
    gpio_set_irq_enabled(12, GPIO_IRQ_EDGE_FALL | GPIO_IRQ_EDGE_RISE, true);
    gpio_set_irq_enabled(13, GPIO_IRQ_EDGE_FALL | GPIO_IRQ_EDGE_RISE, true);
    gpio_set_irq_enabled(14, GPIO_IRQ_EDGE_FALL | GPIO_IRQ_EDGE_RISE, true);
    reset_outputs();

    // Wrap interrupts of every PWM slice share one handler.
    irq_set_exclusive_handler(PWM_DEFAULT_IRQ_NUM(), pwm_wrap_callback);
    irq_set_enabled(PWM_DEFAULT_IRQ_NUM(), true);
}

void enable_gpio(bool enabled)
{
    irq_set_enabled(IO_IRQ_BANK0, enabled);
}

void configure_adc(void)
{
    adc_gpio_init(AI0_PIN);
    adc_gpio_init(AI1_PIN);
    adc_gpio_init(AI2_PIN);

    adc_init();
    adc_set_clkdiv(0); // Run conversion back-to-back at full speed.
    adc_set_round_robin(AI_MASK); // Enable round-robin sampling of all 3 inputs.
    adc_fifo_setup(
        true,    // Write each completed conversion to the sample FIFO
        true,    // Enable DMA data request (DREQ)
        1,       // DREQ (and IRQ) asserted when at least 1 sample present
        false,   // We won't see the ERR bit because of 8 bit reads; disable.
        false    // We won't byte-shift since we will be using the full ADC bit-depth.
    );

    // Get two open DMA channels.
    // adc_sample_channel will sample the adc, paced by DREQ_ADC and chain to adc_ctrl_channel.
    // adc_ctrl_channel will reconfigure & retrigger adc_sample_channel when it finishes.
    adc_sample_channel = dma_claim_unused_channel(true);
    adc_ctrl_channel = dma_claim_unused_channel(true);
    dma_channel_config sample_config = dma_channel_get_default_config(adc_sample_channel);
    dma_channel_config ctrl_config = dma_channel_get_default_config(adc_ctrl_channel);

    // Setup Sample Channel.
    channel_config_set_transfer_data_size(&sample_config, DMA_SIZE_16);
    channel_config_set_read_increment(&sample_config, false); // read from adc FIFO reg.
    channel_config_set_write_increment(&sample_config, true);
    channel_config_set_irq_quiet(&sample_config, true);
    channel_config_set_dreq(&sample_config, DREQ_ADC); // pace data according to ADC
    channel_config_set_chain_to(&sample_config, adc_ctrl_channel);
    channel_config_set_enable(&sample_config, true);

    // Apply adc_sample_channel configuration.
    dma_channel_configure(
        adc_sample_channel, // Channel to be configured
        &sample_config,
        nullptr,            // write (dst) address will be loaded by adc_ctrl_channel.
        &adc_hw->fifo,      // read (source) address. Does not change.
        count_of(adc_vals), // Number of word transfers.
        false               // Don't Start immediately.
    );

    // Setup Reconfiguration Channel
    // This channel will Write the starting address to the write address
    // "trigger" register, which will restart the DMA Sample Channel.
    channel_config_set_transfer_data_size(&ctrl_config, DMA_SIZE_32);
    channel_config_set_read_increment(&ctrl_config, false); // read a single uint32.
    channel_config_set_write_increment(&ctrl_config, false);
    channel_config_set_irq_quiet(&ctrl_config, true);
    channel_config_set_dreq(&ctrl_config, DREQ_FORCE); // Go as fast as possible.
    channel_config_set_enable(&ctrl_config, true);

    // Apply reconfig channel configuration.
    dma_channel_configure(
        adc_ctrl_channel,  // Channel to be configured
        &ctrl_config,
        &dma_hw->ch[adc_sample_channel].al2_write_addr_trig, // dst address. Retrigger on write.
        data_ptr,      // Read (src) address is a single array with the starting address.
        1,             // Number of word transfers.
        false          // Don't Start immediately.
    );

    // Configure queue for storing sample data and avoid concurrency in
    // outbound message buffers, i.e. avoid sending reply in timer callback.
    queue_init(&adc_queue, sizeof(adc_queue_item_t), adc_queue_size);
}

void enable_adc_events()
{
    // Set starting ADC channel for round-robin mode.
    adc_select_input(0);

    // Start free-running ADC and DMA transfer
    dma_channel_start(adc_ctrl_channel);
    adc_run(true);

    // Setup repeating timer for reporting values back to the host.
    add_repeating_timer_us(-adc_callback_delay_us, adc_callback, NULL, &adc_timer);
}

void disable_adc_events()
{
    // Cancel the reporting timer so re-enabling events cannot add it twice.
    cancel_repeating_timer(&adc_timer);

    // Ensure both DMA channels are fully stopped
    // Note: loop is needed since dma_channel_abort does not wait for CHAN_ABORT
    // https://github.com/raspberrypi/pico-sdk/issues/923
    while (dma_channel_is_busy(adc_ctrl_channel) || dma_channel_is_busy(adc_sample_channel)) {
        dma_channel_abort(adc_ctrl_channel);
        dma_channel_abort(adc_sample_channel);
    }

    // Stop the ADC and drain the FIFO.
    adc_run(false);
    adc_fifo_drain();

    // Discard queued samples so they are not sent when events resume.
    while (queue_try_remove(&adc_queue, nullptr));
}

// Send a queued event. A pulse train register reports the end of its train, unless
// a newer write has already replaced it.
void send_queued_event(const event_queue_item_t &event)
{
    if (event.address >= PULSE_TRAIN_GP15_ADDRESS && event.address < PULSE_EVENT_ENABLE_ADDRESS)
    {
        size_t index = event.address - PULSE_TRAIN_GP15_ADDRESS;
        pulse_train_t *pulse_train = &pulse_trains[index];
        if (pulse_train->generation != event.value || pulse_train->running)
            return;
        app_regs.pulse_train[index][0] = 0;
        HarpCore::send_harp_reply(EVENT, event.address, event.timestamp);
        return;
    }

    HarpCore::send_harp_reply(EVENT, event.address, &event.value, sizeof(event.value),
                              reg_type_t::U8, event.timestamp);
}

void update_app_state()
{
    // Enable or disable asynchronous register updates depending on app state
    if (!events_active && HarpCore::events_enabled())
    {
        // enable events
        enable_gpio(true);
        enable_adc_events();
        events_active = true;
    }
    else if (events_active && !HarpCore::events_enabled())
    {
        // disable events
        enable_gpio(false);
        disable_adc_events();
        reset_outputs();
        events_active = false;
    }

    adc_queue_item_t sample;
    if (events_active && queue_try_remove(&adc_queue, &sample))
    {
        app_regs.analog_data[0] = sample.analog_data[0];
        app_regs.analog_data[1] = sample.analog_data[1];
        app_regs.analog_data[2] = sample.analog_data[2];
        HarpCore::send_harp_reply(EVENT, ANALOG_DATA_ADDRESS, sample.timestamp);
    }

    // Drain queued events even while disabled, so the queue never stays full.
    event_queue_item_t event;
    while (queue_try_remove(&event_queue, &event))
    {
        if (events_active)
            send_queued_event(event);
    }
}

// Create Harp App.
HarpCApp& app = HarpCApp::init(HARP_DEVICE_ID, FW_VERSION, HW_VERSION,
                               "Hobgoblin",
                               (const uint8_t*)GIT_HASH, // in CMakeLists.txt.
                               HARP_INTERFACE_HASH,
                               app_reg_specs, app_reg_count, update_app_state,
                               app_reset);

// Core0 main.
int main()
{
    // Init Synchronizer.
    HarpSynchronizer& sync = HarpSynchronizer::init(uart1, HARP_SYNC_RX_PIN);
    app.set_synchronizer(&sync);
    configure_gpio();
    configure_adc();

    while(true)
    {
        app.run();
    }
}
