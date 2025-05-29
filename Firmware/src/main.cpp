#include <cstring>
#include <harp_c_app.h>
#include <harp_synchronizer.h>
#include <core_registers.h>
#include <reg_types.h>
#include <hardware/gpio.h>
#include <hardware/adc.h>
#include <hardware/dma.h>
#include <hardware/pwm.h>

// Create device name array.
const uint16_t who_am_i = 123;
const uint8_t hw_version_major = 1;
const uint8_t hw_version_minor = 0;
const uint8_t assembly_version = 0;
const uint8_t harp_version_major = 2;
const uint8_t harp_version_minor = 0;
const uint8_t fw_version_major = 0;
const uint8_t fw_version_minor = 1;
const uint16_t serial_number = 0x0;

// Harp App setup.
const uint32_t DO0_PIN = 15;
const uint32_t DI_MASK = 0x700C;
const uint32_t DO_MASK = 0xFF << DO0_PIN;
const uint32_t AI0_PIN = 26;
const uint32_t AI1_PIN = 27;
const uint32_t AI2_PIN = 28;
const uint32_t AI_MASK = 0x7;
const uint32_t PWM_PIN = 0;
const uint32_t PWM_SLICE = pwm_gpio_to_slice_num(PWM_PIN);
const uint32_t PWM_CHANNEL = pwm_gpio_to_channel(PWM_PIN);

// Harp App state.
bool events_active = false;

// Repeating timers for pulse control
const size_t pulse_train_count = 256;
struct pulse_train_t
{
    struct repeating_timer timer;
    uint8_t output_mask;
    uint32_t pulse_width_us;
    uint32_t pulse_period_us;
    uint32_t pulse_count;
};
pulse_train_t pulse_train_timers[pulse_train_count];

// Repeating timer and buffers for ADC sampling using 
// Pointer to an address is required for the reinitialization DMA channel.
uint16_t adc_vals[3] = {0, 0, 0};
uint16_t* data_ptr[1] = {adc_vals};
struct repeating_timer adc_timer;
const int32_t adc_period_us = 4000;
const int32_t adc_callback_delay_us = 80000;
int adc_sample_channel;
int adc_ctrl_channel;

// Harp App Register Setup.
const size_t reg_count = 10;

// Define register contents.
#pragma pack(push, 1)
struct app_regs_t
{
    volatile uint8_t di_state; // 32
    volatile uint8_t do_set; // 33
    volatile uint8_t do_clear; // 34
    volatile uint8_t do_toggle; // 35
    volatile uint8_t do_state; // 36
    volatile uint32_t start_pulse_train[4]; // 37
    volatile uint8_t stop_pulse_train; //38
    volatile uint16_t analog_data[3]; // 39
    volatile uint32_t pwm_config[2];  // 40 [0]=frequency in Hz, [1]=duty cycle (0-100)
    volatile uint8_t pwm_stop; // 41
} app_regs;
#pragma pack(pop)

// Define register "specs."
RegSpecs app_reg_specs[reg_count]
{
    {(uint8_t*)&app_regs.di_state, sizeof(app_regs.di_state), U8},
    {(uint8_t*)&app_regs.do_set, sizeof(app_regs.do_set), U8},
    {(uint8_t*)&app_regs.do_clear, sizeof(app_regs.do_clear), U8},
    {(uint8_t*)&app_regs.do_toggle, sizeof(app_regs.do_toggle), U8},
    {(uint8_t*)&app_regs.do_state, sizeof(app_regs.do_state), U8},
    {(uint8_t*)&app_regs.start_pulse_train, sizeof(app_regs.start_pulse_train), U32},
    {(uint8_t*)&app_regs.stop_pulse_train, sizeof(app_regs.stop_pulse_train), U8},
    {(uint8_t*)&app_regs.analog_data, sizeof(app_regs.analog_data), U16},
    {(uint8_t*)&app_regs.pwm_config, sizeof(app_regs.pwm_config), U32},
    {(uint8_t*)&app_regs.pwm_stop, sizeof(app_regs.pwm_stop), U8}
};

void gpio_callback(uint gpio, uint32_t events)
{
    uint32_t gpio_state = gpio_get_all();
    app_regs.di_state = 0;
    app_regs.di_state |= (gpio_state & 0xC) >> 2;
    app_regs.di_state |= (gpio_state & 0x7000) >> 10;
    HarpCore::send_harp_reply(EVENT, APP_REG_START_ADDRESS);
}

void write_do_set(msg_t &msg)
{
    HarpCore::copy_msg_payload_to_register(msg);
    gpio_set_mask(app_regs.do_set << DO0_PIN);
    HarpCore::send_harp_reply(WRITE, msg.header.address);
}

void write_do_clear(msg_t &msg)
{
    HarpCore::copy_msg_payload_to_register(msg);
    gpio_clr_mask(app_regs.do_clear << DO0_PIN);
    HarpCore::send_harp_reply(WRITE, msg.header.address);
}

void write_do_toggle(msg_t &msg)
{
    HarpCore::copy_msg_payload_to_register(msg);
    gpio_xor_mask(app_regs.do_toggle << DO0_PIN);
    HarpCore::send_harp_reply(WRITE, msg.header.address);
}

void write_do_state(msg_t &msg)
{
    HarpCore::copy_msg_payload_to_register(msg);
    gpio_put_masked(DO_MASK, app_regs.do_state << DO0_PIN);
    HarpCore::send_harp_reply(WRITE, msg.header.address);
}

int64_t pulse_callback(alarm_id_t id, void *user_data)
{
    pulse_train_t *pulse_train = (pulse_train_t *)user_data;
    app_regs.do_clear = pulse_train->output_mask;
    gpio_clr_mask(pulse_train->output_mask << DO0_PIN);

    // Emit stop notifications for pulse and pulse train
    uint64_t harp_time_us = HarpCore::harp_time_us_64();
    HarpCore::send_harp_reply(EVENT, APP_REG_START_ADDRESS + 2, harp_time_us);
    if (pulse_train->timer.delay_us == 0)
    {
        // Mark timer as cancelled if pulse train stops
        pulse_train->timer.alarm_id = 0;
        app_regs.stop_pulse_train = pulse_train->output_mask;
        HarpCore::send_harp_reply(EVENT, APP_REG_START_ADDRESS + 6, harp_time_us);
    }
    return 0;
}

bool pulse_train_callback(repeating_timer_t *rt)
{
    pulse_train_t *pulse_train = (pulse_train_t *)rt->user_data;

    // Configure the repeating timer delay following the first pulse
    pulse_train->timer.delay_us = -((int64_t)pulse_train->pulse_period_us);

    // Stop pulse train if positive counter falls to zero;
    // counters which started zero or negative repeat indefinitely
    if (pulse_train->pulse_count > 0 && --pulse_train->pulse_count == 0)
    {
        pulse_train->timer.delay_us = 0;
    }

    // For every pulse in the pulse train, arm an alarm matching the pulse width
    add_alarm_in_us(pulse_train->pulse_width_us, pulse_callback, pulse_train, true);

    gpio_set_mask(pulse_train->output_mask << DO0_PIN);
    HarpCore::send_harp_reply(EVENT, APP_REG_START_ADDRESS + 1);
    return pulse_train->timer.delay_us != 0;
}

void write_start_pulse_train(msg_t& msg)
{
    HarpCore::copy_msg_payload_to_register(msg);

    // Configure pulse train parameters
    uint8_t output_mask = (uint8_t)((app_regs.start_pulse_train[0] & 0xFF));
    pulse_train_t *pulse_train = &pulse_train_timers[output_mask];
    pulse_train->output_mask = output_mask;
    pulse_train->pulse_width_us = app_regs.start_pulse_train[1];
    pulse_train->pulse_period_us = app_regs.start_pulse_train[2];
    pulse_train->pulse_count = app_regs.start_pulse_train[3];
    
    // Cancel any existing timer
    if (cancel_repeating_timer(&pulse_train->timer))
    {
        app_regs.stop_pulse_train = output_mask;
        HarpCore::send_harp_reply(EVENT, APP_REG_START_ADDRESS + 6);
    }

    // Arm repeating timer and immediately arm the first pulse
    HarpCore::send_harp_reply(WRITE, msg.header.address);
    add_repeating_timer_us(0, pulse_train_callback, pulse_train, &pulse_train->timer);
}

void write_stop_pulse_train(msg_t& msg)
{
    HarpCore::copy_msg_payload_to_register(msg);

    uint8_t output_mask = (uint8_t)((app_regs.start_pulse_train[0] & 0xFF));
    cancel_repeating_timer(&pulse_train_timers[output_mask].timer);

    HarpCore::send_harp_reply(WRITE, msg.header.address);
}

bool adc_callback(repeating_timer_t *rt)
{
    if (!HarpCore::events_enabled())
        return false;

    rt->delay_us = -adc_period_us;
    
    // Mask the values to 12 bits (0xFFF) to ensure only valid ADC bits are used
    app_regs.analog_data[0] = adc_vals[0] & 0xFFF;
    app_regs.analog_data[1] = adc_vals[1] & 0xFFF;
    app_regs.analog_data[2] = adc_vals[2] & 0xFFF;
    
    HarpCore::send_harp_reply(EVENT, APP_REG_START_ADDRESS + 7);
    return true;
}

void write_pwm_config(msg_t &msg)
{
    HarpCore::copy_msg_payload_to_register(msg);
    
    uint32_t frequency = app_regs.pwm_config[0];
    uint32_t duty_percent = app_regs.pwm_config[1];
    
    if (duty_percent > 100) duty_percent = 100;
    
    // Assume 125MHz for the 2040
    float clock_div = 125.0f;
    uint32_t wrap = 1000000 / frequency;  // At 1MHz, this gives us cycles per PWM period
    uint32_t level = (wrap * duty_percent) / 100;
    
    pwm_config config = pwm_get_default_config();
    pwm_config_set_clkdiv(&config, clock_div);
    pwm_config_set_wrap(&config, wrap - 1);  // Wrap is 0-based
    
    pwm_init(PWM_SLICE, &config, true);
    pwm_set_chan_level(PWM_SLICE, PWM_CHANNEL, level);
    
    HarpCore::send_harp_reply(WRITE, msg.header.address);
}

void write_pwm_stop(msg_t &msg)
{
    HarpCore::copy_msg_payload_to_register(msg);
    
    pwm_set_enabled(PWM_SLICE, false);
    
    HarpCore::send_harp_reply(WRITE, msg.header.address);
}


// Define register read-and-write handler functions.
RegFnPair reg_handler_fns[reg_count]
{
    {&HarpCore::read_reg_generic, &HarpCore::write_to_read_only_reg_error},
    {&HarpCore::read_reg_generic, &write_do_set},
    {&HarpCore::read_reg_generic, &write_do_clear},
    {&HarpCore::read_reg_generic, &write_do_toggle},
    {&HarpCore::read_reg_generic, &write_do_state},
    {&HarpCore::read_reg_generic, &write_start_pulse_train},
    {&HarpCore::read_reg_generic, &write_stop_pulse_train},
    {&HarpCore::read_reg_generic, &HarpCore::write_to_read_only_reg_error},
    {&HarpCore::read_reg_generic, &write_pwm_config},
    {&HarpCore::read_reg_generic, &write_pwm_stop}
};

void app_reset()
{
    app_regs.di_state = 0;
    app_regs.do_set = 0;
    app_regs.do_clear = 0;
    app_regs.do_toggle = 0;
    app_regs.do_state = 0;
    app_regs.start_pulse_train[0] = 0;
    app_regs.start_pulse_train[1] = 0;
    app_regs.start_pulse_train[2] = 0;
    app_regs.start_pulse_train[3] = 0;
    app_regs.stop_pulse_train = 0;
    app_regs.analog_data[0] = 0;
    app_regs.analog_data[1] = 0;
    app_regs.analog_data[2] = 0;
    app_regs.pwm_config[0] = 1000;
    app_regs.pwm_config[1] = 50;
    app_regs.pwm_stop = 0;

    pwm_set_enabled(PWM_SLICE, false);
}

void configure_gpio(void)
{
    gpio_init_mask(DO_MASK | DI_MASK);
    gpio_set_dir_out_masked(DO_MASK);
    gpio_set_dir_in_masked(DI_MASK);
    gpio_clr_mask(DO_MASK);

    gpio_set_function(PWM_PIN, GPIO_FUNC_PWM);
    
    gpio_set_irq_callback(gpio_callback);
    gpio_set_irq_enabled(2, GPIO_IRQ_EDGE_FALL | GPIO_IRQ_EDGE_RISE, true);
    gpio_set_irq_enabled(3, GPIO_IRQ_EDGE_FALL | GPIO_IRQ_EDGE_RISE, true);
    gpio_set_irq_enabled(12, GPIO_IRQ_EDGE_FALL | GPIO_IRQ_EDGE_RISE, true);
    gpio_set_irq_enabled(13, GPIO_IRQ_EDGE_FALL | GPIO_IRQ_EDGE_RISE, true);
    gpio_set_irq_enabled(14, GPIO_IRQ_EDGE_FALL | GPIO_IRQ_EDGE_RISE, true);
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
}

void cancel_pulse_timers()
{
    // Cancel any pulse train timer which might be still running
    for (size_t i = 0; i < pulse_train_count; i++)
    {
        cancel_repeating_timer(&pulse_train_timers[i].timer);
    }
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
        cancel_pulse_timers();
        events_active = false;
    }
}

// Create Harp App.
HarpCApp& app = HarpCApp::init(who_am_i, hw_version_major, hw_version_minor,
                               assembly_version,
                               harp_version_major, harp_version_minor,
                               fw_version_major, fw_version_minor,
                               serial_number, "Hobgoblin",
                               (const uint8_t*)GIT_HASH, // in CMakeLists.txt.
                               &app_regs, app_reg_specs,
                               reg_handler_fns, reg_count, update_app_state,
                               app_reset);

// Core0 main.
int main()
{
// Init Synchronizer.
    HarpSynchronizer& sync = HarpSynchronizer::init(uart1, 5);
    app.set_synchronizer(&sync);
    configure_gpio();
    configure_adc();
    
    while(true)
    {
        app.run();
    }
}

