# This file was automatically generated and should not be edited directly.
# To make changes, edit the device metadata and regenerate the interface.

import enum
from typing import Any, ClassVar

import numpy as np
from harp.protocol import (
    AnonymousPayload,
    BitMask,
    Field,
    IdentityConverter,
    PayloadType,
    RegisterBase,
    StructPayload,
)
from harp.device.core import REGISTER_MAP as _CORE_REGISTER_MAP


__all__ = [
    "DEVICE_NAME",
    "WHO_AM_I",
    "DigitalInputs",
    "DigitalOutputs",
    "DigitalInputStatePayload",
    "DigitalOutputSetPayload",
    "DigitalOutputClearPayload",
    "DigitalOutputTogglePayload",
    "DigitalOutputStatePayload",
    "StartPulseTrainPayload",
    "StopPulseTrainPayload",
    "AnalogDataPayload",
    "DigitalInputState",
    "DigitalOutputSet",
    "DigitalOutputClear",
    "DigitalOutputToggle",
    "DigitalOutputState",
    "StartPulseTrain",
    "StopPulseTrain",
    "AnalogData",
    "REGISTER_MAP",
]

DEVICE_NAME: str = "Hobgoblin"
WHO_AM_I: int = 123


class DigitalInputs(enum.IntFlag):
    """Specifies the state of port digital input lines."""

    GP2 = 0x1
    """Digital input on pin GP2"""

    GP3 = 0x2
    """Digital input on pin GP3"""

    GP12 = 0x4
    """Digital input on pin GP12"""

    GP13 = 0x8
    """Digital input on pin GP13"""

    GP14 = 0x10
    """Digital input on pin GP14"""


class DigitalOutputs(enum.IntFlag):
    """Specifies the state of port digital output lines."""

    GP15 = 0x1
    """Digital output on pin GP15"""

    GP16 = 0x2
    """Digital output on pin GP16"""

    GP17 = 0x4
    """Digital output on pin GP17"""

    GP18 = 0x8
    """Digital output on pin GP18"""

    GP19 = 0x10
    """Digital output on pin GP19"""

    GP20 = 0x20
    """Digital output on pin GP20"""

    GP21 = 0x40
    """Digital output on pin GP21"""

    GP22 = 0x80
    """Digital output on pin GP22"""


class DigitalInputStatePayload(AnonymousPayload[np.uint8]):
    """Represents the DigitalInputState payload."""

    __value__: DigitalInputs = BitMask(enum=DigitalInputs)


class DigitalOutputSetPayload(AnonymousPayload[np.uint8]):
    """Represents the DigitalOutputSet payload."""

    __value__: DigitalOutputs = BitMask(enum=DigitalOutputs)


class DigitalOutputClearPayload(AnonymousPayload[np.uint8]):
    """Represents the DigitalOutputClear payload."""

    __value__: DigitalOutputs = BitMask(enum=DigitalOutputs)


class DigitalOutputTogglePayload(AnonymousPayload[np.uint8]):
    """Represents the DigitalOutputToggle payload."""

    __value__: DigitalOutputs = BitMask(enum=DigitalOutputs)


class DigitalOutputStatePayload(AnonymousPayload[np.uint8]):
    """Represents the DigitalOutputState payload."""

    __value__: DigitalOutputs = BitMask(enum=DigitalOutputs)


class StartPulseTrainPayload(StructPayload[np.uint32], length=4):
    """Represents the StartPulseTrain payload."""

    digital_output: DigitalOutputs = BitMask(enum=DigitalOutputs, mask=0xFF)
    """Specifies the digital output lines set by each pulse of the pulse train."""

    pulse_width: np.uint32 = Field(
        IdentityConverter(np.uint32), offset=1, default=np.uint32(500000)
    )
    """Specifies the duration in microseconds that each pulse is HIGH."""

    pulse_period: np.uint32 = Field(
        IdentityConverter(np.uint32), offset=2, default=np.uint32(1000000)
    )
    """Specifies the interval in microseconds between each pulse in the pulse train."""

    pulse_count: np.uint32 = Field(IdentityConverter(np.uint32), offset=3, default=np.uint32(1))
    """Specifies the number of pulses in the PWM pulse train. A value of zero signifies an infinite pulse train."""


class StopPulseTrainPayload(AnonymousPayload[np.uint8]):
    """Represents the StopPulseTrain payload."""

    __value__: DigitalOutputs = BitMask(enum=DigitalOutputs)


class AnalogDataPayload(StructPayload[np.uint16], length=3):
    """Represents the AnalogData payload."""

    analog_input0: np.uint16 = Field(IdentityConverter(np.uint16))
    """The analog value sampled from ADC channel 0."""

    analog_input1: np.uint16 = Field(IdentityConverter(np.uint16), offset=1)
    """The analog value sampled from ADC channel 1."""

    analog_input2: np.uint16 = Field(IdentityConverter(np.uint16), offset=2)
    """The analog value sampled from ADC channel 2."""


class DigitalInputState(RegisterBase[DigitalInputs]):
    """Reflects the state of the digital input lines."""

    address: ClassVar[int] = 32
    payload_type: ClassVar[PayloadType] = PayloadType.U8
    payload_class = DigitalInputStatePayload


class DigitalOutputSet(RegisterBase[DigitalOutputs]):
    """Sets the specified digital output lines."""

    address: ClassVar[int] = 33
    payload_type: ClassVar[PayloadType] = PayloadType.U8
    payload_class = DigitalOutputSetPayload


class DigitalOutputClear(RegisterBase[DigitalOutputs]):
    """Clears the specified digital output lines."""

    address: ClassVar[int] = 34
    payload_type: ClassVar[PayloadType] = PayloadType.U8
    payload_class = DigitalOutputClearPayload


class DigitalOutputToggle(RegisterBase[DigitalOutputs]):
    """Toggles the specified digital output lines."""

    address: ClassVar[int] = 35
    payload_type: ClassVar[PayloadType] = PayloadType.U8
    payload_class = DigitalOutputTogglePayload


class DigitalOutputState(RegisterBase[DigitalOutputs]):
    """Writes the state of all digital output lines."""

    address: ClassVar[int] = 36
    payload_type: ClassVar[PayloadType] = PayloadType.U8
    payload_class = DigitalOutputStatePayload


class StartPulseTrain(RegisterBase[StartPulseTrainPayload]):
    """Starts a pulse train driving the specified digital output lines."""

    address: ClassVar[int] = 37
    payload_type: ClassVar[PayloadType] = PayloadType.U32
    payload_class = StartPulseTrainPayload


class StopPulseTrain(RegisterBase[DigitalOutputs]):
    """Stops the pulse train running on the specified digital output lines."""

    address: ClassVar[int] = 38
    payload_type: ClassVar[PayloadType] = PayloadType.U8
    payload_class = StopPulseTrainPayload


class AnalogData(RegisterBase[AnalogDataPayload]):
    """Reports the sampled analog signal on each of the ADC input channels. The ADC is capped at 12 bits of resolution."""

    address: ClassVar[int] = 39
    payload_type: ClassVar[PayloadType] = PayloadType.U16
    payload_class = AnalogDataPayload


REGISTER_MAP: dict[int, type[RegisterBase[Any]]] = {
    **_CORE_REGISTER_MAP,
    32: DigitalInputState,
    33: DigitalOutputSet,
    34: DigitalOutputClear,
    35: DigitalOutputToggle,
    36: DigitalOutputState,
    37: StartPulseTrain,
    38: StopPulseTrain,
    39: AnalogData,
}
