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
    "AnalogDataPayload",
    "PulseTrainGP15Payload",
    "PulseTrainGP16Payload",
    "PulseTrainGP17Payload",
    "PulseTrainGP18Payload",
    "PulseTrainGP19Payload",
    "PulseTrainGP20Payload",
    "PulseTrainGP21Payload",
    "PulseTrainGP22Payload",
    "PulseEventEnablePayload",
    "DigitalInputState",
    "DigitalOutputSet",
    "DigitalOutputClear",
    "DigitalOutputToggle",
    "DigitalOutputState",
    "AnalogData",
    "PulseTrainGP15",
    "PulseTrainGP16",
    "PulseTrainGP17",
    "PulseTrainGP18",
    "PulseTrainGP19",
    "PulseTrainGP20",
    "PulseTrainGP21",
    "PulseTrainGP22",
    "PulseEventEnable",
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


class AnalogDataPayload(StructPayload[np.uint16], length=3):
    """Represents the AnalogData payload."""

    analog_input0: np.uint16 = Field(IdentityConverter(np.uint16))
    """The analog value sampled from ADC channel 0."""

    analog_input1: np.uint16 = Field(IdentityConverter(np.uint16), offset=1)
    """The analog value sampled from ADC channel 1."""

    analog_input2: np.uint16 = Field(IdentityConverter(np.uint16), offset=2)
    """The analog value sampled from ADC channel 2."""


class PulseTrainGP15Payload(StructPayload[np.uint32], length=3):
    """Represents the PulseTrainGP15 payload."""

    pulse_width: np.uint32 = Field(IdentityConverter(np.uint32), default=np.uint32(500000))
    """Specifies the duration in microseconds that each pulse is high. A value of zero stops the pulse train."""

    pulse_period: np.uint32 = Field(
        IdentityConverter(np.uint32), offset=1, default=np.uint32(1000000)
    )
    """Specifies the interval in microseconds between the start of each pulse."""

    pulse_count: np.uint32 = Field(IdentityConverter(np.uint32), offset=2, default=np.uint32(1))
    """Specifies the number of pulses in the pulse train. A value of zero runs the pulse train until it is stopped."""


class PulseTrainGP16Payload(StructPayload[np.uint32], length=3):
    """Represents the PulseTrainGP16 payload."""

    pulse_width: np.uint32 = Field(IdentityConverter(np.uint32), default=np.uint32(500000))
    """Specifies the duration in microseconds that each pulse is high. A value of zero stops the pulse train."""

    pulse_period: np.uint32 = Field(
        IdentityConverter(np.uint32), offset=1, default=np.uint32(1000000)
    )
    """Specifies the interval in microseconds between the start of each pulse."""

    pulse_count: np.uint32 = Field(IdentityConverter(np.uint32), offset=2, default=np.uint32(1))
    """Specifies the number of pulses in the pulse train. A value of zero runs the pulse train until it is stopped."""


class PulseTrainGP17Payload(StructPayload[np.uint32], length=3):
    """Represents the PulseTrainGP17 payload."""

    pulse_width: np.uint32 = Field(IdentityConverter(np.uint32), default=np.uint32(500000))
    """Specifies the duration in microseconds that each pulse is high. A value of zero stops the pulse train."""

    pulse_period: np.uint32 = Field(
        IdentityConverter(np.uint32), offset=1, default=np.uint32(1000000)
    )
    """Specifies the interval in microseconds between the start of each pulse."""

    pulse_count: np.uint32 = Field(IdentityConverter(np.uint32), offset=2, default=np.uint32(1))
    """Specifies the number of pulses in the pulse train. A value of zero runs the pulse train until it is stopped."""


class PulseTrainGP18Payload(StructPayload[np.uint32], length=3):
    """Represents the PulseTrainGP18 payload."""

    pulse_width: np.uint32 = Field(IdentityConverter(np.uint32), default=np.uint32(500000))
    """Specifies the duration in microseconds that each pulse is high. A value of zero stops the pulse train."""

    pulse_period: np.uint32 = Field(
        IdentityConverter(np.uint32), offset=1, default=np.uint32(1000000)
    )
    """Specifies the interval in microseconds between the start of each pulse."""

    pulse_count: np.uint32 = Field(IdentityConverter(np.uint32), offset=2, default=np.uint32(1))
    """Specifies the number of pulses in the pulse train. A value of zero runs the pulse train until it is stopped."""


class PulseTrainGP19Payload(StructPayload[np.uint32], length=3):
    """Represents the PulseTrainGP19 payload."""

    pulse_width: np.uint32 = Field(IdentityConverter(np.uint32), default=np.uint32(500000))
    """Specifies the duration in microseconds that each pulse is high. A value of zero stops the pulse train."""

    pulse_period: np.uint32 = Field(
        IdentityConverter(np.uint32), offset=1, default=np.uint32(1000000)
    )
    """Specifies the interval in microseconds between the start of each pulse."""

    pulse_count: np.uint32 = Field(IdentityConverter(np.uint32), offset=2, default=np.uint32(1))
    """Specifies the number of pulses in the pulse train. A value of zero runs the pulse train until it is stopped."""


class PulseTrainGP20Payload(StructPayload[np.uint32], length=3):
    """Represents the PulseTrainGP20 payload."""

    pulse_width: np.uint32 = Field(IdentityConverter(np.uint32), default=np.uint32(500000))
    """Specifies the duration in microseconds that each pulse is high. A value of zero stops the pulse train."""

    pulse_period: np.uint32 = Field(
        IdentityConverter(np.uint32), offset=1, default=np.uint32(1000000)
    )
    """Specifies the interval in microseconds between the start of each pulse."""

    pulse_count: np.uint32 = Field(IdentityConverter(np.uint32), offset=2, default=np.uint32(1))
    """Specifies the number of pulses in the pulse train. A value of zero runs the pulse train until it is stopped."""


class PulseTrainGP21Payload(StructPayload[np.uint32], length=3):
    """Represents the PulseTrainGP21 payload."""

    pulse_width: np.uint32 = Field(IdentityConverter(np.uint32), default=np.uint32(500000))
    """Specifies the duration in microseconds that each pulse is high. A value of zero stops the pulse train."""

    pulse_period: np.uint32 = Field(
        IdentityConverter(np.uint32), offset=1, default=np.uint32(1000000)
    )
    """Specifies the interval in microseconds between the start of each pulse."""

    pulse_count: np.uint32 = Field(IdentityConverter(np.uint32), offset=2, default=np.uint32(1))
    """Specifies the number of pulses in the pulse train. A value of zero runs the pulse train until it is stopped."""


class PulseTrainGP22Payload(StructPayload[np.uint32], length=3):
    """Represents the PulseTrainGP22 payload."""

    pulse_width: np.uint32 = Field(IdentityConverter(np.uint32), default=np.uint32(500000))
    """Specifies the duration in microseconds that each pulse is high. A value of zero stops the pulse train."""

    pulse_period: np.uint32 = Field(
        IdentityConverter(np.uint32), offset=1, default=np.uint32(1000000)
    )
    """Specifies the interval in microseconds between the start of each pulse."""

    pulse_count: np.uint32 = Field(IdentityConverter(np.uint32), offset=2, default=np.uint32(1))
    """Specifies the number of pulses in the pulse train. A value of zero runs the pulse train until it is stopped."""


class PulseEventEnablePayload(AnonymousPayload[np.uint8]):
    """Represents the PulseEventEnable payload."""

    __value__: DigitalOutputs = BitMask(
        enum=DigitalOutputs,
        default=DigitalOutputs.GP15 | DigitalOutputs.GP16 | DigitalOutputs.GP17 | DigitalOutputs.GP18 | DigitalOutputs.GP19 | DigitalOutputs.GP20 | DigitalOutputs.GP21 | DigitalOutputs.GP22,
    )


class DigitalInputState(RegisterBase[DigitalInputs]):
    """Reflects the state of the digital input lines."""

    address: ClassVar[int] = 32
    payload_type: ClassVar[PayloadType] = PayloadType.U8
    payload_class = DigitalInputStatePayload


class DigitalOutputSet(RegisterBase[DigitalOutputs]):
    """Sets the specified output lines and reports each pulse start. Setting a line stops its pulse train."""

    address: ClassVar[int] = 33
    payload_type: ClassVar[PayloadType] = PayloadType.U8
    payload_class = DigitalOutputSetPayload


class DigitalOutputClear(RegisterBase[DigitalOutputs]):
    """Clears the specified output lines. Clearing a line stops its pulse train."""

    address: ClassVar[int] = 34
    payload_type: ClassVar[PayloadType] = PayloadType.U8
    payload_class = DigitalOutputClearPayload


class DigitalOutputToggle(RegisterBase[DigitalOutputs]):
    """Toggles the specified output lines. Toggling a line stops its pulse train."""

    address: ClassVar[int] = 35
    payload_type: ClassVar[PayloadType] = PayloadType.U8
    payload_class = DigitalOutputTogglePayload


class DigitalOutputState(RegisterBase[DigitalOutputs]):
    """Writes the state of all output lines. Writing the state stops every pulse train."""

    address: ClassVar[int] = 36
    payload_type: ClassVar[PayloadType] = PayloadType.U8
    payload_class = DigitalOutputStatePayload


class AnalogData(RegisterBase[AnalogDataPayload]):
    """Reports the sampled analog signal on each of the ADC input channels. The ADC is capped at 12 bits of resolution."""

    address: ClassVar[int] = 37
    payload_type: ClassVar[PayloadType] = PayloadType.U16
    payload_class = AnalogDataPayload


class PulseTrainGP15(RegisterBase[PulseTrainGP15Payload]):
    """Starts a pulse train on digital output GP15, or stops it if the pulse width is zero."""

    address: ClassVar[int] = 38
    payload_type: ClassVar[PayloadType] = PayloadType.U32
    payload_class = PulseTrainGP15Payload


class PulseTrainGP16(RegisterBase[PulseTrainGP16Payload]):
    """Starts a pulse train on digital output GP16, or stops it if the pulse width is zero."""

    address: ClassVar[int] = 39
    payload_type: ClassVar[PayloadType] = PayloadType.U32
    payload_class = PulseTrainGP16Payload


class PulseTrainGP17(RegisterBase[PulseTrainGP17Payload]):
    """Starts a pulse train on digital output GP17, or stops it if the pulse width is zero."""

    address: ClassVar[int] = 40
    payload_type: ClassVar[PayloadType] = PayloadType.U32
    payload_class = PulseTrainGP17Payload


class PulseTrainGP18(RegisterBase[PulseTrainGP18Payload]):
    """Starts a pulse train on digital output GP18, or stops it if the pulse width is zero."""

    address: ClassVar[int] = 41
    payload_type: ClassVar[PayloadType] = PayloadType.U32
    payload_class = PulseTrainGP18Payload


class PulseTrainGP19(RegisterBase[PulseTrainGP19Payload]):
    """Starts a pulse train on digital output GP19, or stops it if the pulse width is zero."""

    address: ClassVar[int] = 42
    payload_type: ClassVar[PayloadType] = PayloadType.U32
    payload_class = PulseTrainGP19Payload


class PulseTrainGP20(RegisterBase[PulseTrainGP20Payload]):
    """Starts a pulse train on digital output GP20, or stops it if the pulse width is zero."""

    address: ClassVar[int] = 43
    payload_type: ClassVar[PayloadType] = PayloadType.U32
    payload_class = PulseTrainGP20Payload


class PulseTrainGP21(RegisterBase[PulseTrainGP21Payload]):
    """Starts a pulse train on digital output GP21, or stops it if the pulse width is zero."""

    address: ClassVar[int] = 44
    payload_type: ClassVar[PayloadType] = PayloadType.U32
    payload_class = PulseTrainGP21Payload


class PulseTrainGP22(RegisterBase[PulseTrainGP22Payload]):
    """Starts a pulse train on digital output GP22, or stops it if the pulse width is zero."""

    address: ClassVar[int] = 45
    payload_type: ClassVar[PayloadType] = PayloadType.U32
    payload_class = PulseTrainGP22Payload


class PulseEventEnable(RegisterBase[DigitalOutputs]):
    """Specifies the digital output lines whose pulse trains report the start of each pulse on DigitalOutputSet."""

    address: ClassVar[int] = 46
    payload_type: ClassVar[PayloadType] = PayloadType.U8
    payload_class = PulseEventEnablePayload


REGISTER_MAP: dict[int, type[RegisterBase[Any]]] = {
    **_CORE_REGISTER_MAP,
    32: DigitalInputState,
    33: DigitalOutputSet,
    34: DigitalOutputClear,
    35: DigitalOutputToggle,
    36: DigitalOutputState,
    37: AnalogData,
    38: PulseTrainGP15,
    39: PulseTrainGP16,
    40: PulseTrainGP17,
    41: PulseTrainGP18,
    42: PulseTrainGP19,
    43: PulseTrainGP20,
    44: PulseTrainGP21,
    45: PulseTrainGP22,
    46: PulseEventEnable,
}
