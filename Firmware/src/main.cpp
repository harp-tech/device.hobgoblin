#include <cstring>
#include <harp_c_app.h>
#include <harp_synchronizer.h>
#include <core_registers.h>
#include <reg_types.h>
#include <hardware/gpio.h>

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

// Harp App Register Setup.
const size_t reg_count = 7;

// Hobgoblin device setup
const uint32_t DO0_PIN = 15;
const uint32_t DI_MASK = 0x700C;
const uint32_t DO_MASK = 0xFF << DO0_PIN;

// Repeating timers for pulse control
const size_t pulse_train_count = 256;
struct pulse_train_t
{
    struct repeating_timer timer;
    uint8_t pwm_output;
    uint32_t pulse_width_us;
    uint32_t pulse_period_us;
    uint32_t pulse_count;
};
pulse_train_t pulse_train_timers[pulse_train_count];

// Define register contents.
#pragma pack(push, 1)
struct app_regs_t
{
    volatile uint8_t di_state;
    volatile uint8_t do_set;
    volatile uint8_t do_clear;
    volatile uint8_t do_toggle;
    volatile uint8_t do_state;
    volatile uint32_t start_pulse_train[4];
    volatile uint8_t stop_pulse_train;
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
    app_regs.do_clear = pulse_train->pwm_output;
    gpio_clr_mask(pulse_train->pwm_output << DO0_PIN);

    // Emit stop notifications for pulse and pulse train
    uint64_t harp_time_us = HarpCore::harp_time_us_64();
    HarpCore::send_harp_reply(EVENT, APP_REG_START_ADDRESS + 2, harp_time_us);
    if (pulse_train->timer.delay_us == 0)
    {
        // Mark timer as cancelled if pulse train stops
        pulse_train->timer.alarm_id = 0;
        app_regs.stop_pulse_train = pulse_train->pwm_output;
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

    gpio_set_mask(pulse_train->pwm_output << DO0_PIN);
    HarpCore::send_harp_reply(EVENT, APP_REG_START_ADDRESS + 1);
    return pulse_train->timer.delay_us != 0;
}

void write_start_pulse_train(msg_t& msg)
{
    HarpCore::copy_msg_payload_to_register(msg);

    // Configure pulse train parameters
    uint8_t pwm_output = (uint8_t)((app_regs.start_pulse_train[0] & 0xFF));
    pulse_train_t *pulse_train = &pulse_train_timers[pwm_output];
    pulse_train->pwm_output = pwm_output;
    pulse_train->pulse_width_us = app_regs.start_pulse_train[1];
    pulse_train->pulse_period_us = app_regs.start_pulse_train[2];
    pulse_train->pulse_count = app_regs.start_pulse_train[3];
    
    // Cancel any existing timer
    if (cancel_repeating_timer(&pulse_train->timer))
    {
        app_regs.stop_pulse_train = pwm_output;
        HarpCore::send_harp_reply(EVENT, APP_REG_START_ADDRESS + 6);
    }

    // Arm repeating timer and immediately arm the first pulse
    HarpCore::send_harp_reply(WRITE, msg.header.address);
    add_repeating_timer_us(0, pulse_train_callback, pulse_train, &pulse_train->timer);
}

void write_stop_pulse_train(msg_t& msg)
{
    HarpCore::copy_msg_payload_to_register(msg);

    uint8_t pwm_output = (uint8_t)((app_regs.start_pulse_train[0] & 0xFF));
    cancel_repeating_timer(&pulse_train_timers[pwm_output].timer);

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
    {&HarpCore::read_reg_generic, &write_stop_pulse_train}
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
}

void update_app_state()
{
    // update here!
    // If app registers update their states outside the read/write handler
    // functions, update them here.
    // (Called inside run() function.)
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

void configure_gpio(void)
{
    gpio_init_mask(DO_MASK | DI_MASK);
    gpio_set_dir_out_masked(DO_MASK);
    gpio_set_dir_in_masked(DI_MASK);
    gpio_clr_mask(DO_MASK);

    gpio_set_irq_callback(gpio_callback);
    gpio_set_irq_enabled(2, GPIO_IRQ_EDGE_FALL | GPIO_IRQ_EDGE_RISE, true);
    gpio_set_irq_enabled(3, GPIO_IRQ_EDGE_FALL | GPIO_IRQ_EDGE_RISE, true);
    gpio_set_irq_enabled(12, GPIO_IRQ_EDGE_FALL | GPIO_IRQ_EDGE_RISE, true);
    gpio_set_irq_enabled(13, GPIO_IRQ_EDGE_FALL | GPIO_IRQ_EDGE_RISE, true);
    gpio_set_irq_enabled(14, GPIO_IRQ_EDGE_FALL | GPIO_IRQ_EDGE_RISE, true);
    irq_set_enabled(IO_IRQ_BANK0, true);
}

// Core0 main.
int main()
{
// Init Synchronizer.
    HarpSynchronizer& sync = HarpSynchronizer::init(uart1, 5);
    app.set_synchronizer(&sync);
    configure_gpio();
    
    while(true)
    {
        app.run();
    }
}
