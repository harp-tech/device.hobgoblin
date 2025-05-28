using Bonsai;
using Bonsai.Harp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Xml.Serialization;

namespace Harp.Hobgoblin
{
    /// <summary>
    /// Generates events and processes commands for the Hobgoblin device connected
    /// at the specified serial port.
    /// </summary>
    [Combinator(MethodName = nameof(Generate))]
    [WorkflowElementCategory(ElementCategory.Source)]
    [Description("Generates events and processes commands for the Hobgoblin device.")]
    public partial class Device : Bonsai.Harp.Device, INamedElement
    {
        /// <summary>
        /// Represents the unique identity class of the <see cref="Hobgoblin"/> device.
        /// This field is constant.
        /// </summary>
        public const int WhoAmI = 123;

        /// <summary>
        /// Initializes a new instance of the <see cref="Device"/> class.
        /// </summary>
        public Device() : base(WhoAmI) { }

        string INamedElement.Name => nameof(Hobgoblin);

        /// <summary>
        /// Gets a read-only mapping from address to register type.
        /// </summary>
        public static new IReadOnlyDictionary<int, Type> RegisterMap { get; } = new Dictionary<int, Type>
            (Bonsai.Harp.Device.RegisterMap.ToDictionary(entry => entry.Key, entry => entry.Value))
        {
            { 32, typeof(DigitalInputState) },
            { 33, typeof(DigitalOutputSet) },
            { 34, typeof(DigitalOutputClear) },
            { 35, typeof(DigitalOutputToggle) },
            { 36, typeof(DigitalOutputState) },
            { 37, typeof(StartPulseTrain) },
            { 38, typeof(StopPulseTrain) },
            { 39, typeof(AnalogData) }
        };

        /// <summary>
        /// Gets the contents of the metadata file describing the <see cref="Hobgoblin"/>
        /// device registers.
        /// </summary>
        public static readonly string Metadata = GetDeviceMetadata();

        static string GetDeviceMetadata()
        {
            var deviceType = typeof(Device);
            using var metadataStream = deviceType.Assembly.GetManifestResourceStream($"{deviceType.Namespace}.device.yml");
            using var streamReader = new System.IO.StreamReader(metadataStream);
            return streamReader.ReadToEnd();
        }
    }

    /// <summary>
    /// Represents an operator that returns the contents of the metadata file
    /// describing the <see cref="Hobgoblin"/> device registers.
    /// </summary>
    [Description("Returns the contents of the metadata file describing the Hobgoblin device registers.")]
    public partial class GetDeviceMetadata : Source<string>
    {
        /// <summary>
        /// Returns an observable sequence with the contents of the metadata file
        /// describing the <see cref="Hobgoblin"/> device registers.
        /// </summary>
        /// <returns>
        /// A sequence with a single <see cref="string"/> object representing the
        /// contents of the metadata file.
        /// </returns>
        public override IObservable<string> Generate()
        {
            return Observable.Return(Device.Metadata);
        }
    }

    /// <summary>
    /// Represents an operator that groups the sequence of <see cref="Hobgoblin"/>" messages by register type.
    /// </summary>
    [Description("Groups the sequence of Hobgoblin messages by register type.")]
    public partial class GroupByRegister : Combinator<HarpMessage, IGroupedObservable<Type, HarpMessage>>
    {
        /// <summary>
        /// Groups an observable sequence of <see cref="Hobgoblin"/> messages
        /// by register type.
        /// </summary>
        /// <param name="source">The sequence of Harp device messages.</param>
        /// <returns>
        /// A sequence of observable groups, each of which corresponds to a unique
        /// <see cref="Hobgoblin"/> register.
        /// </returns>
        public override IObservable<IGroupedObservable<Type, HarpMessage>> Process(IObservable<HarpMessage> source)
        {
            return source.GroupBy(message => Device.RegisterMap[message.Address]);
        }
    }

    /// <summary>
    /// Represents an operator that writes the sequence of <see cref="Hobgoblin"/>" messages
    /// to the standard Harp storage format.
    /// </summary>
    [Description("Writes the sequence of Hobgoblin messages to the standard Harp storage format.")]
    public partial class DeviceDataWriter : Sink<HarpMessage>, INamedElement
    {
        const string BinaryExtension = ".bin";
        const string MetadataFileName = "device.yml";
        readonly Bonsai.Harp.MessageWriter writer = new();

        string INamedElement.Name => nameof(Hobgoblin) + "DataWriter";

        /// <summary>
        /// Gets or sets the relative or absolute path on which to save the message data.
        /// </summary>
        [Description("The relative or absolute path of the directory on which to save the message data.")]
        [Editor("Bonsai.Design.SaveFileNameEditor, Bonsai.Design", DesignTypes.UITypeEditor)]
        public string Path
        {
            get => System.IO.Path.GetDirectoryName(writer.FileName);
            set => writer.FileName = System.IO.Path.Combine(value, nameof(Hobgoblin) + BinaryExtension);
        }

        /// <summary>
        /// Gets or sets a value indicating whether element writing should be buffered. If <see langword="true"/>,
        /// the write commands will be queued in memory as fast as possible and will be processed
        /// by the writer in a different thread. Otherwise, writing will be done in the same
        /// thread in which notifications arrive.
        /// </summary>
        [Description("Indicates whether writing should be buffered.")]
        public bool Buffered
        {
            get => writer.Buffered;
            set => writer.Buffered = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to overwrite the output file if it already exists.
        /// </summary>
        [Description("Indicates whether to overwrite the output file if it already exists.")]
        public bool Overwrite
        {
            get => writer.Overwrite;
            set => writer.Overwrite = value;
        }

        /// <summary>
        /// Gets or sets a value specifying how the message filter will use the matching criteria.
        /// </summary>
        [Description("Specifies how the message filter will use the matching criteria.")]
        public FilterType FilterType
        {
            get => writer.FilterType;
            set => writer.FilterType = value;
        }

        /// <summary>
        /// Gets or sets a value specifying the expected message type. If no value is
        /// specified, all messages will be accepted.
        /// </summary>
        [Description("Specifies the expected message type. If no value is specified, all messages will be accepted.")]
        public MessageType? MessageType
        {
            get => writer.MessageType;
            set => writer.MessageType = value;
        }

        private IObservable<TSource> WriteDeviceMetadata<TSource>(IObservable<TSource> source)
        {
            var basePath = Path;
            if (string.IsNullOrEmpty(basePath))
                return source;

            var metadataPath = System.IO.Path.Combine(basePath, MetadataFileName);
            return Observable.Create<TSource>(observer =>
            {
                Bonsai.IO.PathHelper.EnsureDirectory(metadataPath);
                if (System.IO.File.Exists(metadataPath) && !Overwrite)
                {
                    throw new System.IO.IOException(string.Format("The file '{0}' already exists.", metadataPath));
                }

                System.IO.File.WriteAllText(metadataPath, Device.Metadata);
                return source.SubscribeSafe(observer);
            });
        }

        /// <summary>
        /// Writes each Harp message in the sequence to the specified binary file, and the
        /// contents of the device metadata file to a separate text file.
        /// </summary>
        /// <param name="source">The sequence of messages to write to the file.</param>
        /// <returns>
        /// An observable sequence that is identical to the <paramref name="source"/>
        /// sequence but where there is an additional side effect of writing the
        /// messages to a raw binary file, and the contents of the device metadata file
        /// to a separate text file.
        /// </returns>
        public override IObservable<HarpMessage> Process(IObservable<HarpMessage> source)
        {
            return source.Publish(ps => ps.Merge(
                WriteDeviceMetadata(writer.Process(ps.GroupBy(message => message.Address)))
                .IgnoreElements()
                .Cast<HarpMessage>()));
        }

        /// <summary>
        /// Writes each Harp message in the sequence of observable groups to the
        /// corresponding binary file, where the name of each file is generated from
        /// the common group register address. The contents of the device metadata file are
        /// written to a separate text file.
        /// </summary>
        /// <param name="source">
        /// A sequence of observable groups, each of which corresponds to a unique register
        /// address.
        /// </param>
        /// <returns>
        /// An observable sequence that is identical to the <paramref name="source"/>
        /// sequence but where there is an additional side effect of writing the Harp
        /// messages in each group to the corresponding file, and the contents of the device
        /// metadata file to a separate text file.
        /// </returns>
        public IObservable<IGroupedObservable<int, HarpMessage>> Process(IObservable<IGroupedObservable<int, HarpMessage>> source)
        {
            return WriteDeviceMetadata(writer.Process(source));
        }

        /// <summary>
        /// Writes each Harp message in the sequence of observable groups to the
        /// corresponding binary file, where the name of each file is generated from
        /// the common group register name. The contents of the device metadata file are
        /// written to a separate text file.
        /// </summary>
        /// <param name="source">
        /// A sequence of observable groups, each of which corresponds to a unique register
        /// type.
        /// </param>
        /// <returns>
        /// An observable sequence that is identical to the <paramref name="source"/>
        /// sequence but where there is an additional side effect of writing the Harp
        /// messages in each group to the corresponding file, and the contents of the device
        /// metadata file to a separate text file.
        /// </returns>
        public IObservable<IGroupedObservable<Type, HarpMessage>> Process(IObservable<IGroupedObservable<Type, HarpMessage>> source)
        {
            return WriteDeviceMetadata(writer.Process(source));
        }
    }

    /// <summary>
    /// Represents an operator that filters register-specific messages
    /// reported by the <see cref="Hobgoblin"/> device.
    /// </summary>
    /// <seealso cref="DigitalInputState"/>
    /// <seealso cref="DigitalOutputSet"/>
    /// <seealso cref="DigitalOutputClear"/>
    /// <seealso cref="DigitalOutputToggle"/>
    /// <seealso cref="DigitalOutputState"/>
    /// <seealso cref="StartPulseTrain"/>
    /// <seealso cref="StopPulseTrain"/>
    /// <seealso cref="AnalogData"/>
    [XmlInclude(typeof(DigitalInputState))]
    [XmlInclude(typeof(DigitalOutputSet))]
    [XmlInclude(typeof(DigitalOutputClear))]
    [XmlInclude(typeof(DigitalOutputToggle))]
    [XmlInclude(typeof(DigitalOutputState))]
    [XmlInclude(typeof(StartPulseTrain))]
    [XmlInclude(typeof(StopPulseTrain))]
    [XmlInclude(typeof(AnalogData))]
    [Description("Filters register-specific messages reported by the Hobgoblin device.")]
    public class FilterRegister : FilterRegisterBuilder, INamedElement
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FilterRegister"/> class.
        /// </summary>
        public FilterRegister()
        {
            Register = new DigitalInputState();
        }

        string INamedElement.Name
        {
            get => $"{nameof(Hobgoblin)}.{GetElementDisplayName(Register)}";
        }
    }

    /// <summary>
    /// Represents an operator which filters and selects specific messages
    /// reported by the Hobgoblin device.
    /// </summary>
    /// <seealso cref="DigitalInputState"/>
    /// <seealso cref="DigitalOutputSet"/>
    /// <seealso cref="DigitalOutputClear"/>
    /// <seealso cref="DigitalOutputToggle"/>
    /// <seealso cref="DigitalOutputState"/>
    /// <seealso cref="StartPulseTrain"/>
    /// <seealso cref="StopPulseTrain"/>
    /// <seealso cref="AnalogData"/>
    [XmlInclude(typeof(DigitalInputState))]
    [XmlInclude(typeof(DigitalOutputSet))]
    [XmlInclude(typeof(DigitalOutputClear))]
    [XmlInclude(typeof(DigitalOutputToggle))]
    [XmlInclude(typeof(DigitalOutputState))]
    [XmlInclude(typeof(StartPulseTrain))]
    [XmlInclude(typeof(StopPulseTrain))]
    [XmlInclude(typeof(AnalogData))]
    [XmlInclude(typeof(TimestampedDigitalInputState))]
    [XmlInclude(typeof(TimestampedDigitalOutputSet))]
    [XmlInclude(typeof(TimestampedDigitalOutputClear))]
    [XmlInclude(typeof(TimestampedDigitalOutputToggle))]
    [XmlInclude(typeof(TimestampedDigitalOutputState))]
    [XmlInclude(typeof(TimestampedStartPulseTrain))]
    [XmlInclude(typeof(TimestampedStopPulseTrain))]
    [XmlInclude(typeof(TimestampedAnalogData))]
    [Description("Filters and selects specific messages reported by the Hobgoblin device.")]
    public partial class Parse : ParseBuilder, INamedElement
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Parse"/> class.
        /// </summary>
        public Parse()
        {
            Register = new DigitalInputState();
        }

        string INamedElement.Name => $"{nameof(Hobgoblin)}.{GetElementDisplayName(Register)}";
    }

    /// <summary>
    /// Represents an operator which formats a sequence of values as specific
    /// Hobgoblin register messages.
    /// </summary>
    /// <seealso cref="DigitalInputState"/>
    /// <seealso cref="DigitalOutputSet"/>
    /// <seealso cref="DigitalOutputClear"/>
    /// <seealso cref="DigitalOutputToggle"/>
    /// <seealso cref="DigitalOutputState"/>
    /// <seealso cref="StartPulseTrain"/>
    /// <seealso cref="StopPulseTrain"/>
    /// <seealso cref="AnalogData"/>
    [XmlInclude(typeof(DigitalInputState))]
    [XmlInclude(typeof(DigitalOutputSet))]
    [XmlInclude(typeof(DigitalOutputClear))]
    [XmlInclude(typeof(DigitalOutputToggle))]
    [XmlInclude(typeof(DigitalOutputState))]
    [XmlInclude(typeof(StartPulseTrain))]
    [XmlInclude(typeof(StopPulseTrain))]
    [XmlInclude(typeof(AnalogData))]
    [Description("Formats a sequence of values as specific Hobgoblin register messages.")]
    public partial class Format : FormatBuilder, INamedElement
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Format"/> class.
        /// </summary>
        public Format()
        {
            Register = new DigitalInputState();
        }

        string INamedElement.Name => $"{nameof(Hobgoblin)}.{GetElementDisplayName(Register)}";
    }

    /// <summary>
    /// Represents a register that reflects the state of the digital input lines.
    /// </summary>
    [Description("Reflects the state of the digital input lines.")]
    public partial class DigitalInputState
    {
        /// <summary>
        /// Represents the address of the <see cref="DigitalInputState"/> register. This field is constant.
        /// </summary>
        public const int Address = 32;

        /// <summary>
        /// Represents the payload type of the <see cref="DigitalInputState"/> register. This field is constant.
        /// </summary>
        public const PayloadType RegisterType = PayloadType.U8;

        /// <summary>
        /// Represents the length of the <see cref="DigitalInputState"/> register. This field is constant.
        /// </summary>
        public const int RegisterLength = 1;

        /// <summary>
        /// Returns the payload data for <see cref="DigitalInputState"/> register messages.
        /// </summary>
        /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
        /// <returns>A value representing the message payload.</returns>
        public static DigitalInputs GetPayload(HarpMessage message)
        {
            return (DigitalInputs)message.GetPayloadByte();
        }

        /// <summary>
        /// Returns the timestamped payload data for <see cref="DigitalInputState"/> register messages.
        /// </summary>
        /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
        /// <returns>A value representing the timestamped message payload.</returns>
        public static Timestamped<DigitalInputs> GetTimestampedPayload(HarpMessage message)
        {
            var payload = message.GetTimestampedPayloadByte();
            return Timestamped.Create((DigitalInputs)payload.Value, payload.Seconds);
        }

        /// <summary>
        /// Returns a Harp message for the <see cref="DigitalInputState"/> register.
        /// </summary>
        /// <param name="messageType">The type of the Harp message.</param>
        /// <param name="value">The value to be stored in the message payload.</param>
        /// <returns>
        /// A <see cref="HarpMessage"/> object for the <see cref="DigitalInputState"/> register
        /// with the specified message type and payload.
        /// </returns>
        public static HarpMessage FromPayload(MessageType messageType, DigitalInputs value)
        {
            return HarpMessage.FromByte(Address, messageType, (byte)value);
        }

        /// <summary>
        /// Returns a timestamped Harp message for the <see cref="DigitalInputState"/>
        /// register.
        /// </summary>
        /// <param name="timestamp">The timestamp of the message payload, in seconds.</param>
        /// <param name="messageType">The type of the Harp message.</param>
        /// <param name="value">The value to be stored in the message payload.</param>
        /// <returns>
        /// A <see cref="HarpMessage"/> object for the <see cref="DigitalInputState"/> register
        /// with the specified message type, timestamp, and payload.
        /// </returns>
        public static HarpMessage FromPayload(double timestamp, MessageType messageType, DigitalInputs value)
        {
            return HarpMessage.FromByte(Address, timestamp, messageType, (byte)value);
        }
    }

    /// <summary>
    /// Provides methods for manipulating timestamped messages from the
    /// DigitalInputState register.
    /// </summary>
    /// <seealso cref="DigitalInputState"/>
    [Description("Filters and selects timestamped messages from the DigitalInputState register.")]
    public partial class TimestampedDigitalInputState
    {
        /// <summary>
        /// Represents the address of the <see cref="DigitalInputState"/> register. This field is constant.
        /// </summary>
        public const int Address = DigitalInputState.Address;

        /// <summary>
        /// Returns timestamped payload data for <see cref="DigitalInputState"/> register messages.
        /// </summary>
        /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
        /// <returns>A value representing the timestamped message payload.</returns>
        public static Timestamped<DigitalInputs> GetPayload(HarpMessage message)
        {
            return DigitalInputState.GetTimestampedPayload(message);
        }
    }

    /// <summary>
    /// Represents a register that sets the specified digital output lines.
    /// </summary>
    [Description("Sets the specified digital output lines.")]
    public partial class DigitalOutputSet
    {
        /// <summary>
        /// Represents the address of the <see cref="DigitalOutputSet"/> register. This field is constant.
        /// </summary>
        public const int Address = 33;

        /// <summary>
        /// Represents the payload type of the <see cref="DigitalOutputSet"/> register. This field is constant.
        /// </summary>
        public const PayloadType RegisterType = PayloadType.U8;

        /// <summary>
        /// Represents the length of the <see cref="DigitalOutputSet"/> register. This field is constant.
        /// </summary>
        public const int RegisterLength = 1;

        /// <summary>
        /// Returns the payload data for <see cref="DigitalOutputSet"/> register messages.
        /// </summary>
        /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
        /// <returns>A value representing the message payload.</returns>
        public static DigitalOutputs GetPayload(HarpMessage message)
        {
            return (DigitalOutputs)message.GetPayloadByte();
        }

        /// <summary>
        /// Returns the timestamped payload data for <see cref="DigitalOutputSet"/> register messages.
        /// </summary>
        /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
        /// <returns>A value representing the timestamped message payload.</returns>
        public static Timestamped<DigitalOutputs> GetTimestampedPayload(HarpMessage message)
        {
            var payload = message.GetTimestampedPayloadByte();
            return Timestamped.Create((DigitalOutputs)payload.Value, payload.Seconds);
        }

        /// <summary>
        /// Returns a Harp message for the <see cref="DigitalOutputSet"/> register.
        /// </summary>
        /// <param name="messageType">The type of the Harp message.</param>
        /// <param name="value">The value to be stored in the message payload.</param>
        /// <returns>
        /// A <see cref="HarpMessage"/> object for the <see cref="DigitalOutputSet"/> register
        /// with the specified message type and payload.
        /// </returns>
        public static HarpMessage FromPayload(MessageType messageType, DigitalOutputs value)
        {
            return HarpMessage.FromByte(Address, messageType, (byte)value);
        }

        /// <summary>
        /// Returns a timestamped Harp message for the <see cref="DigitalOutputSet"/>
        /// register.
        /// </summary>
        /// <param name="timestamp">The timestamp of the message payload, in seconds.</param>
        /// <param name="messageType">The type of the Harp message.</param>
        /// <param name="value">The value to be stored in the message payload.</param>
        /// <returns>
        /// A <see cref="HarpMessage"/> object for the <see cref="DigitalOutputSet"/> register
        /// with the specified message type, timestamp, and payload.
        /// </returns>
        public static HarpMessage FromPayload(double timestamp, MessageType messageType, DigitalOutputs value)
        {
            return HarpMessage.FromByte(Address, timestamp, messageType, (byte)value);
        }
    }

    /// <summary>
    /// Provides methods for manipulating timestamped messages from the
    /// DigitalOutputSet register.
    /// </summary>
    /// <seealso cref="DigitalOutputSet"/>
    [Description("Filters and selects timestamped messages from the DigitalOutputSet register.")]
    public partial class TimestampedDigitalOutputSet
    {
        /// <summary>
        /// Represents the address of the <see cref="DigitalOutputSet"/> register. This field is constant.
        /// </summary>
        public const int Address = DigitalOutputSet.Address;

        /// <summary>
        /// Returns timestamped payload data for <see cref="DigitalOutputSet"/> register messages.
        /// </summary>
        /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
        /// <returns>A value representing the timestamped message payload.</returns>
        public static Timestamped<DigitalOutputs> GetPayload(HarpMessage message)
        {
            return DigitalOutputSet.GetTimestampedPayload(message);
        }
    }

    /// <summary>
    /// Represents a register that clears the specified digital output lines.
    /// </summary>
    [Description("Clears the specified digital output lines.")]
    public partial class DigitalOutputClear
    {
        /// <summary>
        /// Represents the address of the <see cref="DigitalOutputClear"/> register. This field is constant.
        /// </summary>
        public const int Address = 34;

        /// <summary>
        /// Represents the payload type of the <see cref="DigitalOutputClear"/> register. This field is constant.
        /// </summary>
        public const PayloadType RegisterType = PayloadType.U8;

        /// <summary>
        /// Represents the length of the <see cref="DigitalOutputClear"/> register. This field is constant.
        /// </summary>
        public const int RegisterLength = 1;

        /// <summary>
        /// Returns the payload data for <see cref="DigitalOutputClear"/> register messages.
        /// </summary>
        /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
        /// <returns>A value representing the message payload.</returns>
        public static DigitalOutputs GetPayload(HarpMessage message)
        {
            return (DigitalOutputs)message.GetPayloadByte();
        }

        /// <summary>
        /// Returns the timestamped payload data for <see cref="DigitalOutputClear"/> register messages.
        /// </summary>
        /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
        /// <returns>A value representing the timestamped message payload.</returns>
        public static Timestamped<DigitalOutputs> GetTimestampedPayload(HarpMessage message)
        {
            var payload = message.GetTimestampedPayloadByte();
            return Timestamped.Create((DigitalOutputs)payload.Value, payload.Seconds);
        }

        /// <summary>
        /// Returns a Harp message for the <see cref="DigitalOutputClear"/> register.
        /// </summary>
        /// <param name="messageType">The type of the Harp message.</param>
        /// <param name="value">The value to be stored in the message payload.</param>
        /// <returns>
        /// A <see cref="HarpMessage"/> object for the <see cref="DigitalOutputClear"/> register
        /// with the specified message type and payload.
        /// </returns>
        public static HarpMessage FromPayload(MessageType messageType, DigitalOutputs value)
        {
            return HarpMessage.FromByte(Address, messageType, (byte)value);
        }

        /// <summary>
        /// Returns a timestamped Harp message for the <see cref="DigitalOutputClear"/>
        /// register.
        /// </summary>
        /// <param name="timestamp">The timestamp of the message payload, in seconds.</param>
        /// <param name="messageType">The type of the Harp message.</param>
        /// <param name="value">The value to be stored in the message payload.</param>
        /// <returns>
        /// A <see cref="HarpMessage"/> object for the <see cref="DigitalOutputClear"/> register
        /// with the specified message type, timestamp, and payload.
        /// </returns>
        public static HarpMessage FromPayload(double timestamp, MessageType messageType, DigitalOutputs value)
        {
            return HarpMessage.FromByte(Address, timestamp, messageType, (byte)value);
        }
    }

    /// <summary>
    /// Provides methods for manipulating timestamped messages from the
    /// DigitalOutputClear register.
    /// </summary>
    /// <seealso cref="DigitalOutputClear"/>
    [Description("Filters and selects timestamped messages from the DigitalOutputClear register.")]
    public partial class TimestampedDigitalOutputClear
    {
        /// <summary>
        /// Represents the address of the <see cref="DigitalOutputClear"/> register. This field is constant.
        /// </summary>
        public const int Address = DigitalOutputClear.Address;

        /// <summary>
        /// Returns timestamped payload data for <see cref="DigitalOutputClear"/> register messages.
        /// </summary>
        /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
        /// <returns>A value representing the timestamped message payload.</returns>
        public static Timestamped<DigitalOutputs> GetPayload(HarpMessage message)
        {
            return DigitalOutputClear.GetTimestampedPayload(message);
        }
    }

    /// <summary>
    /// Represents a register that toggles the specified digital output lines.
    /// </summary>
    [Description("Toggles the specified digital output lines.")]
    public partial class DigitalOutputToggle
    {
        /// <summary>
        /// Represents the address of the <see cref="DigitalOutputToggle"/> register. This field is constant.
        /// </summary>
        public const int Address = 35;

        /// <summary>
        /// Represents the payload type of the <see cref="DigitalOutputToggle"/> register. This field is constant.
        /// </summary>
        public const PayloadType RegisterType = PayloadType.U8;

        /// <summary>
        /// Represents the length of the <see cref="DigitalOutputToggle"/> register. This field is constant.
        /// </summary>
        public const int RegisterLength = 1;

        /// <summary>
        /// Returns the payload data for <see cref="DigitalOutputToggle"/> register messages.
        /// </summary>
        /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
        /// <returns>A value representing the message payload.</returns>
        public static DigitalOutputs GetPayload(HarpMessage message)
        {
            return (DigitalOutputs)message.GetPayloadByte();
        }

        /// <summary>
        /// Returns the timestamped payload data for <see cref="DigitalOutputToggle"/> register messages.
        /// </summary>
        /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
        /// <returns>A value representing the timestamped message payload.</returns>
        public static Timestamped<DigitalOutputs> GetTimestampedPayload(HarpMessage message)
        {
            var payload = message.GetTimestampedPayloadByte();
            return Timestamped.Create((DigitalOutputs)payload.Value, payload.Seconds);
        }

        /// <summary>
        /// Returns a Harp message for the <see cref="DigitalOutputToggle"/> register.
        /// </summary>
        /// <param name="messageType">The type of the Harp message.</param>
        /// <param name="value">The value to be stored in the message payload.</param>
        /// <returns>
        /// A <see cref="HarpMessage"/> object for the <see cref="DigitalOutputToggle"/> register
        /// with the specified message type and payload.
        /// </returns>
        public static HarpMessage FromPayload(MessageType messageType, DigitalOutputs value)
        {
            return HarpMessage.FromByte(Address, messageType, (byte)value);
        }

        /// <summary>
        /// Returns a timestamped Harp message for the <see cref="DigitalOutputToggle"/>
        /// register.
        /// </summary>
        /// <param name="timestamp">The timestamp of the message payload, in seconds.</param>
        /// <param name="messageType">The type of the Harp message.</param>
        /// <param name="value">The value to be stored in the message payload.</param>
        /// <returns>
        /// A <see cref="HarpMessage"/> object for the <see cref="DigitalOutputToggle"/> register
        /// with the specified message type, timestamp, and payload.
        /// </returns>
        public static HarpMessage FromPayload(double timestamp, MessageType messageType, DigitalOutputs value)
        {
            return HarpMessage.FromByte(Address, timestamp, messageType, (byte)value);
        }
    }

    /// <summary>
    /// Provides methods for manipulating timestamped messages from the
    /// DigitalOutputToggle register.
    /// </summary>
    /// <seealso cref="DigitalOutputToggle"/>
    [Description("Filters and selects timestamped messages from the DigitalOutputToggle register.")]
    public partial class TimestampedDigitalOutputToggle
    {
        /// <summary>
        /// Represents the address of the <see cref="DigitalOutputToggle"/> register. This field is constant.
        /// </summary>
        public const int Address = DigitalOutputToggle.Address;

        /// <summary>
        /// Returns timestamped payload data for <see cref="DigitalOutputToggle"/> register messages.
        /// </summary>
        /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
        /// <returns>A value representing the timestamped message payload.</returns>
        public static Timestamped<DigitalOutputs> GetPayload(HarpMessage message)
        {
            return DigitalOutputToggle.GetTimestampedPayload(message);
        }
    }

    /// <summary>
    /// Represents a register that writes the state of all digital output lines.
    /// </summary>
    [Description("Writes the state of all digital output lines.")]
    public partial class DigitalOutputState
    {
        /// <summary>
        /// Represents the address of the <see cref="DigitalOutputState"/> register. This field is constant.
        /// </summary>
        public const int Address = 36;

        /// <summary>
        /// Represents the payload type of the <see cref="DigitalOutputState"/> register. This field is constant.
        /// </summary>
        public const PayloadType RegisterType = PayloadType.U8;

        /// <summary>
        /// Represents the length of the <see cref="DigitalOutputState"/> register. This field is constant.
        /// </summary>
        public const int RegisterLength = 1;

        /// <summary>
        /// Returns the payload data for <see cref="DigitalOutputState"/> register messages.
        /// </summary>
        /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
        /// <returns>A value representing the message payload.</returns>
        public static DigitalOutputs GetPayload(HarpMessage message)
        {
            return (DigitalOutputs)message.GetPayloadByte();
        }

        /// <summary>
        /// Returns the timestamped payload data for <see cref="DigitalOutputState"/> register messages.
        /// </summary>
        /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
        /// <returns>A value representing the timestamped message payload.</returns>
        public static Timestamped<DigitalOutputs> GetTimestampedPayload(HarpMessage message)
        {
            var payload = message.GetTimestampedPayloadByte();
            return Timestamped.Create((DigitalOutputs)payload.Value, payload.Seconds);
        }

        /// <summary>
        /// Returns a Harp message for the <see cref="DigitalOutputState"/> register.
        /// </summary>
        /// <param name="messageType">The type of the Harp message.</param>
        /// <param name="value">The value to be stored in the message payload.</param>
        /// <returns>
        /// A <see cref="HarpMessage"/> object for the <see cref="DigitalOutputState"/> register
        /// with the specified message type and payload.
        /// </returns>
        public static HarpMessage FromPayload(MessageType messageType, DigitalOutputs value)
        {
            return HarpMessage.FromByte(Address, messageType, (byte)value);
        }

        /// <summary>
        /// Returns a timestamped Harp message for the <see cref="DigitalOutputState"/>
        /// register.
        /// </summary>
        /// <param name="timestamp">The timestamp of the message payload, in seconds.</param>
        /// <param name="messageType">The type of the Harp message.</param>
        /// <param name="value">The value to be stored in the message payload.</param>
        /// <returns>
        /// A <see cref="HarpMessage"/> object for the <see cref="DigitalOutputState"/> register
        /// with the specified message type, timestamp, and payload.
        /// </returns>
        public static HarpMessage FromPayload(double timestamp, MessageType messageType, DigitalOutputs value)
        {
            return HarpMessage.FromByte(Address, timestamp, messageType, (byte)value);
        }
    }

    /// <summary>
    /// Provides methods for manipulating timestamped messages from the
    /// DigitalOutputState register.
    /// </summary>
    /// <seealso cref="DigitalOutputState"/>
    [Description("Filters and selects timestamped messages from the DigitalOutputState register.")]
    public partial class TimestampedDigitalOutputState
    {
        /// <summary>
        /// Represents the address of the <see cref="DigitalOutputState"/> register. This field is constant.
        /// </summary>
        public const int Address = DigitalOutputState.Address;

        /// <summary>
        /// Returns timestamped payload data for <see cref="DigitalOutputState"/> register messages.
        /// </summary>
        /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
        /// <returns>A value representing the timestamped message payload.</returns>
        public static Timestamped<DigitalOutputs> GetPayload(HarpMessage message)
        {
            return DigitalOutputState.GetTimestampedPayload(message);
        }
    }

    /// <summary>
    /// Represents a register that starts a pulse train driving the specified digital output lines.
    /// </summary>
    [Description("Starts a pulse train driving the specified digital output lines.")]
    public partial class StartPulseTrain
    {
        /// <summary>
        /// Represents the address of the <see cref="StartPulseTrain"/> register. This field is constant.
        /// </summary>
        public const int Address = 37;

        /// <summary>
        /// Represents the payload type of the <see cref="StartPulseTrain"/> register. This field is constant.
        /// </summary>
        public const PayloadType RegisterType = PayloadType.U32;

        /// <summary>
        /// Represents the length of the <see cref="StartPulseTrain"/> register. This field is constant.
        /// </summary>
        public const int RegisterLength = 4;

        static StartPulseTrainPayload ParsePayload(uint[] payload)
        {
            StartPulseTrainPayload result;
            result.DigitalOutput = (DigitalOutputs)(uint)(payload[0] & 0xFF);
            result.PulseWidth = payload[1];
            result.PulsePeriod = payload[2];
            result.PulseCount = payload[3];
            return result;
        }

        static uint[] FormatPayload(StartPulseTrainPayload value)
        {
            uint[] result;
            result = new uint[4];
            result[0] = (uint)((uint)value.DigitalOutput & 0xFF);
            result[1] = value.PulseWidth;
            result[2] = value.PulsePeriod;
            result[3] = value.PulseCount;
            return result;
        }

        /// <summary>
        /// Returns the payload data for <see cref="StartPulseTrain"/> register messages.
        /// </summary>
        /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
        /// <returns>A value representing the message payload.</returns>
        public static StartPulseTrainPayload GetPayload(HarpMessage message)
        {
            return ParsePayload(message.GetPayloadArray<uint>());
        }

        /// <summary>
        /// Returns the timestamped payload data for <see cref="StartPulseTrain"/> register messages.
        /// </summary>
        /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
        /// <returns>A value representing the timestamped message payload.</returns>
        public static Timestamped<StartPulseTrainPayload> GetTimestampedPayload(HarpMessage message)
        {
            var payload = message.GetTimestampedPayloadArray<uint>();
            return Timestamped.Create(ParsePayload(payload.Value), payload.Seconds);
        }

        /// <summary>
        /// Returns a Harp message for the <see cref="StartPulseTrain"/> register.
        /// </summary>
        /// <param name="messageType">The type of the Harp message.</param>
        /// <param name="value">The value to be stored in the message payload.</param>
        /// <returns>
        /// A <see cref="HarpMessage"/> object for the <see cref="StartPulseTrain"/> register
        /// with the specified message type and payload.
        /// </returns>
        public static HarpMessage FromPayload(MessageType messageType, StartPulseTrainPayload value)
        {
            return HarpMessage.FromUInt32(Address, messageType, FormatPayload(value));
        }

        /// <summary>
        /// Returns a timestamped Harp message for the <see cref="StartPulseTrain"/>
        /// register.
        /// </summary>
        /// <param name="timestamp">The timestamp of the message payload, in seconds.</param>
        /// <param name="messageType">The type of the Harp message.</param>
        /// <param name="value">The value to be stored in the message payload.</param>
        /// <returns>
        /// A <see cref="HarpMessage"/> object for the <see cref="StartPulseTrain"/> register
        /// with the specified message type, timestamp, and payload.
        /// </returns>
        public static HarpMessage FromPayload(double timestamp, MessageType messageType, StartPulseTrainPayload value)
        {
            return HarpMessage.FromUInt32(Address, timestamp, messageType, FormatPayload(value));
        }
    }

    /// <summary>
    /// Provides methods for manipulating timestamped messages from the
    /// StartPulseTrain register.
    /// </summary>
    /// <seealso cref="StartPulseTrain"/>
    [Description("Filters and selects timestamped messages from the StartPulseTrain register.")]
    public partial class TimestampedStartPulseTrain
    {
        /// <summary>
        /// Represents the address of the <see cref="StartPulseTrain"/> register. This field is constant.
        /// </summary>
        public const int Address = StartPulseTrain.Address;

        /// <summary>
        /// Returns timestamped payload data for <see cref="StartPulseTrain"/> register messages.
        /// </summary>
        /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
        /// <returns>A value representing the timestamped message payload.</returns>
        public static Timestamped<StartPulseTrainPayload> GetPayload(HarpMessage message)
        {
            return StartPulseTrain.GetTimestampedPayload(message);
        }
    }

    /// <summary>
    /// Represents a register that stops the pulse train running on the specified digital output lines.
    /// </summary>
    [Description("Stops the pulse train running on the specified digital output lines.")]
    public partial class StopPulseTrain
    {
        /// <summary>
        /// Represents the address of the <see cref="StopPulseTrain"/> register. This field is constant.
        /// </summary>
        public const int Address = 38;

        /// <summary>
        /// Represents the payload type of the <see cref="StopPulseTrain"/> register. This field is constant.
        /// </summary>
        public const PayloadType RegisterType = PayloadType.U8;

        /// <summary>
        /// Represents the length of the <see cref="StopPulseTrain"/> register. This field is constant.
        /// </summary>
        public const int RegisterLength = 1;

        /// <summary>
        /// Returns the payload data for <see cref="StopPulseTrain"/> register messages.
        /// </summary>
        /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
        /// <returns>A value representing the message payload.</returns>
        public static DigitalOutputs GetPayload(HarpMessage message)
        {
            return (DigitalOutputs)message.GetPayloadByte();
        }

        /// <summary>
        /// Returns the timestamped payload data for <see cref="StopPulseTrain"/> register messages.
        /// </summary>
        /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
        /// <returns>A value representing the timestamped message payload.</returns>
        public static Timestamped<DigitalOutputs> GetTimestampedPayload(HarpMessage message)
        {
            var payload = message.GetTimestampedPayloadByte();
            return Timestamped.Create((DigitalOutputs)payload.Value, payload.Seconds);
        }

        /// <summary>
        /// Returns a Harp message for the <see cref="StopPulseTrain"/> register.
        /// </summary>
        /// <param name="messageType">The type of the Harp message.</param>
        /// <param name="value">The value to be stored in the message payload.</param>
        /// <returns>
        /// A <see cref="HarpMessage"/> object for the <see cref="StopPulseTrain"/> register
        /// with the specified message type and payload.
        /// </returns>
        public static HarpMessage FromPayload(MessageType messageType, DigitalOutputs value)
        {
            return HarpMessage.FromByte(Address, messageType, (byte)value);
        }

        /// <summary>
        /// Returns a timestamped Harp message for the <see cref="StopPulseTrain"/>
        /// register.
        /// </summary>
        /// <param name="timestamp">The timestamp of the message payload, in seconds.</param>
        /// <param name="messageType">The type of the Harp message.</param>
        /// <param name="value">The value to be stored in the message payload.</param>
        /// <returns>
        /// A <see cref="HarpMessage"/> object for the <see cref="StopPulseTrain"/> register
        /// with the specified message type, timestamp, and payload.
        /// </returns>
        public static HarpMessage FromPayload(double timestamp, MessageType messageType, DigitalOutputs value)
        {
            return HarpMessage.FromByte(Address, timestamp, messageType, (byte)value);
        }
    }

    /// <summary>
    /// Provides methods for manipulating timestamped messages from the
    /// StopPulseTrain register.
    /// </summary>
    /// <seealso cref="StopPulseTrain"/>
    [Description("Filters and selects timestamped messages from the StopPulseTrain register.")]
    public partial class TimestampedStopPulseTrain
    {
        /// <summary>
        /// Represents the address of the <see cref="StopPulseTrain"/> register. This field is constant.
        /// </summary>
        public const int Address = StopPulseTrain.Address;

        /// <summary>
        /// Returns timestamped payload data for <see cref="StopPulseTrain"/> register messages.
        /// </summary>
        /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
        /// <returns>A value representing the timestamped message payload.</returns>
        public static Timestamped<DigitalOutputs> GetPayload(HarpMessage message)
        {
            return StopPulseTrain.GetTimestampedPayload(message);
        }
    }

    /// <summary>
    /// Represents a register that reports the sampled analog signal on each of the ADC input channels.
    /// </summary>
    [Description("Reports the sampled analog signal on each of the ADC input channels.")]
    public partial class AnalogData
    {
        /// <summary>
        /// Represents the address of the <see cref="AnalogData"/> register. This field is constant.
        /// </summary>
        public const int Address = 39;

        /// <summary>
        /// Represents the payload type of the <see cref="AnalogData"/> register. This field is constant.
        /// </summary>
        public const PayloadType RegisterType = PayloadType.U8;

        /// <summary>
        /// Represents the length of the <see cref="AnalogData"/> register. This field is constant.
        /// </summary>
        public const int RegisterLength = 3;

        static AnalogDataPayload ParsePayload(byte[] payload)
        {
            AnalogDataPayload result;
            result.AnalogInput0 = payload[0];
            result.AnalogInput1 = payload[1];
            result.AnalogInput2 = payload[2];
            return result;
        }

        static byte[] FormatPayload(AnalogDataPayload value)
        {
            byte[] result;
            result = new byte[3];
            result[0] = value.AnalogInput0;
            result[1] = value.AnalogInput1;
            result[2] = value.AnalogInput2;
            return result;
        }

        /// <summary>
        /// Returns the payload data for <see cref="AnalogData"/> register messages.
        /// </summary>
        /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
        /// <returns>A value representing the message payload.</returns>
        public static AnalogDataPayload GetPayload(HarpMessage message)
        {
            return ParsePayload(message.GetPayloadArray<byte>());
        }

        /// <summary>
        /// Returns the timestamped payload data for <see cref="AnalogData"/> register messages.
        /// </summary>
        /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
        /// <returns>A value representing the timestamped message payload.</returns>
        public static Timestamped<AnalogDataPayload> GetTimestampedPayload(HarpMessage message)
        {
            var payload = message.GetTimestampedPayloadArray<byte>();
            return Timestamped.Create(ParsePayload(payload.Value), payload.Seconds);
        }

        /// <summary>
        /// Returns a Harp message for the <see cref="AnalogData"/> register.
        /// </summary>
        /// <param name="messageType">The type of the Harp message.</param>
        /// <param name="value">The value to be stored in the message payload.</param>
        /// <returns>
        /// A <see cref="HarpMessage"/> object for the <see cref="AnalogData"/> register
        /// with the specified message type and payload.
        /// </returns>
        public static HarpMessage FromPayload(MessageType messageType, AnalogDataPayload value)
        {
            return HarpMessage.FromByte(Address, messageType, FormatPayload(value));
        }

        /// <summary>
        /// Returns a timestamped Harp message for the <see cref="AnalogData"/>
        /// register.
        /// </summary>
        /// <param name="timestamp">The timestamp of the message payload, in seconds.</param>
        /// <param name="messageType">The type of the Harp message.</param>
        /// <param name="value">The value to be stored in the message payload.</param>
        /// <returns>
        /// A <see cref="HarpMessage"/> object for the <see cref="AnalogData"/> register
        /// with the specified message type, timestamp, and payload.
        /// </returns>
        public static HarpMessage FromPayload(double timestamp, MessageType messageType, AnalogDataPayload value)
        {
            return HarpMessage.FromByte(Address, timestamp, messageType, FormatPayload(value));
        }
    }

    /// <summary>
    /// Provides methods for manipulating timestamped messages from the
    /// AnalogData register.
    /// </summary>
    /// <seealso cref="AnalogData"/>
    [Description("Filters and selects timestamped messages from the AnalogData register.")]
    public partial class TimestampedAnalogData
    {
        /// <summary>
        /// Represents the address of the <see cref="AnalogData"/> register. This field is constant.
        /// </summary>
        public const int Address = AnalogData.Address;

        /// <summary>
        /// Returns timestamped payload data for <see cref="AnalogData"/> register messages.
        /// </summary>
        /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
        /// <returns>A value representing the timestamped message payload.</returns>
        public static Timestamped<AnalogDataPayload> GetPayload(HarpMessage message)
        {
            return AnalogData.GetTimestampedPayload(message);
        }
    }

    /// <summary>
    /// Represents an operator which creates standard message payloads for the
    /// Hobgoblin device.
    /// </summary>
    /// <seealso cref="CreateDigitalInputStatePayload"/>
    /// <seealso cref="CreateDigitalOutputSetPayload"/>
    /// <seealso cref="CreateDigitalOutputClearPayload"/>
    /// <seealso cref="CreateDigitalOutputTogglePayload"/>
    /// <seealso cref="CreateDigitalOutputStatePayload"/>
    /// <seealso cref="CreateStartPulseTrainPayload"/>
    /// <seealso cref="CreateStopPulseTrainPayload"/>
    /// <seealso cref="CreateAnalogDataPayload"/>
    [XmlInclude(typeof(CreateDigitalInputStatePayload))]
    [XmlInclude(typeof(CreateDigitalOutputSetPayload))]
    [XmlInclude(typeof(CreateDigitalOutputClearPayload))]
    [XmlInclude(typeof(CreateDigitalOutputTogglePayload))]
    [XmlInclude(typeof(CreateDigitalOutputStatePayload))]
    [XmlInclude(typeof(CreateStartPulseTrainPayload))]
    [XmlInclude(typeof(CreateStopPulseTrainPayload))]
    [XmlInclude(typeof(CreateAnalogDataPayload))]
    [XmlInclude(typeof(CreateTimestampedDigitalInputStatePayload))]
    [XmlInclude(typeof(CreateTimestampedDigitalOutputSetPayload))]
    [XmlInclude(typeof(CreateTimestampedDigitalOutputClearPayload))]
    [XmlInclude(typeof(CreateTimestampedDigitalOutputTogglePayload))]
    [XmlInclude(typeof(CreateTimestampedDigitalOutputStatePayload))]
    [XmlInclude(typeof(CreateTimestampedStartPulseTrainPayload))]
    [XmlInclude(typeof(CreateTimestampedStopPulseTrainPayload))]
    [XmlInclude(typeof(CreateTimestampedAnalogDataPayload))]
    [Description("Creates standard message payloads for the Hobgoblin device.")]
    public partial class CreateMessage : CreateMessageBuilder, INamedElement
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CreateMessage"/> class.
        /// </summary>
        public CreateMessage()
        {
            Payload = new CreateDigitalInputStatePayload();
        }

        string INamedElement.Name => $"{nameof(Hobgoblin)}.{GetElementDisplayName(Payload)}";
    }

    /// <summary>
    /// Represents an operator that creates a message payload
    /// that reflects the state of the digital input lines.
    /// </summary>
    [DisplayName("DigitalInputStatePayload")]
    [Description("Creates a message payload that reflects the state of the digital input lines.")]
    public partial class CreateDigitalInputStatePayload
    {
        /// <summary>
        /// Gets or sets the value that reflects the state of the digital input lines.
        /// </summary>
        [Description("The value that reflects the state of the digital input lines.")]
        public DigitalInputs DigitalInputState { get; set; }

        /// <summary>
        /// Creates a message payload for the DigitalInputState register.
        /// </summary>
        /// <returns>The created message payload value.</returns>
        public DigitalInputs GetPayload()
        {
            return DigitalInputState;
        }

        /// <summary>
        /// Creates a message that reflects the state of the digital input lines.
        /// </summary>
        /// <param name="messageType">Specifies the type of the created message.</param>
        /// <returns>A new message for the DigitalInputState register.</returns>
        public HarpMessage GetMessage(MessageType messageType)
        {
            return Harp.Hobgoblin.DigitalInputState.FromPayload(messageType, GetPayload());
        }
    }

    /// <summary>
    /// Represents an operator that creates a timestamped message payload
    /// that reflects the state of the digital input lines.
    /// </summary>
    [DisplayName("TimestampedDigitalInputStatePayload")]
    [Description("Creates a timestamped message payload that reflects the state of the digital input lines.")]
    public partial class CreateTimestampedDigitalInputStatePayload : CreateDigitalInputStatePayload
    {
        /// <summary>
        /// Creates a timestamped message that reflects the state of the digital input lines.
        /// </summary>
        /// <param name="timestamp">The timestamp of the message payload, in seconds.</param>
        /// <param name="messageType">Specifies the type of the created message.</param>
        /// <returns>A new timestamped message for the DigitalInputState register.</returns>
        public HarpMessage GetMessage(double timestamp, MessageType messageType)
        {
            return Harp.Hobgoblin.DigitalInputState.FromPayload(timestamp, messageType, GetPayload());
        }
    }

    /// <summary>
    /// Represents an operator that creates a message payload
    /// that sets the specified digital output lines.
    /// </summary>
    [DisplayName("DigitalOutputSetPayload")]
    [Description("Creates a message payload that sets the specified digital output lines.")]
    public partial class CreateDigitalOutputSetPayload
    {
        /// <summary>
        /// Gets or sets the value that sets the specified digital output lines.
        /// </summary>
        [Description("The value that sets the specified digital output lines.")]
        public DigitalOutputs DigitalOutputSet { get; set; }

        /// <summary>
        /// Creates a message payload for the DigitalOutputSet register.
        /// </summary>
        /// <returns>The created message payload value.</returns>
        public DigitalOutputs GetPayload()
        {
            return DigitalOutputSet;
        }

        /// <summary>
        /// Creates a message that sets the specified digital output lines.
        /// </summary>
        /// <param name="messageType">Specifies the type of the created message.</param>
        /// <returns>A new message for the DigitalOutputSet register.</returns>
        public HarpMessage GetMessage(MessageType messageType)
        {
            return Harp.Hobgoblin.DigitalOutputSet.FromPayload(messageType, GetPayload());
        }
    }

    /// <summary>
    /// Represents an operator that creates a timestamped message payload
    /// that sets the specified digital output lines.
    /// </summary>
    [DisplayName("TimestampedDigitalOutputSetPayload")]
    [Description("Creates a timestamped message payload that sets the specified digital output lines.")]
    public partial class CreateTimestampedDigitalOutputSetPayload : CreateDigitalOutputSetPayload
    {
        /// <summary>
        /// Creates a timestamped message that sets the specified digital output lines.
        /// </summary>
        /// <param name="timestamp">The timestamp of the message payload, in seconds.</param>
        /// <param name="messageType">Specifies the type of the created message.</param>
        /// <returns>A new timestamped message for the DigitalOutputSet register.</returns>
        public HarpMessage GetMessage(double timestamp, MessageType messageType)
        {
            return Harp.Hobgoblin.DigitalOutputSet.FromPayload(timestamp, messageType, GetPayload());
        }
    }

    /// <summary>
    /// Represents an operator that creates a message payload
    /// that clears the specified digital output lines.
    /// </summary>
    [DisplayName("DigitalOutputClearPayload")]
    [Description("Creates a message payload that clears the specified digital output lines.")]
    public partial class CreateDigitalOutputClearPayload
    {
        /// <summary>
        /// Gets or sets the value that clears the specified digital output lines.
        /// </summary>
        [Description("The value that clears the specified digital output lines.")]
        public DigitalOutputs DigitalOutputClear { get; set; }

        /// <summary>
        /// Creates a message payload for the DigitalOutputClear register.
        /// </summary>
        /// <returns>The created message payload value.</returns>
        public DigitalOutputs GetPayload()
        {
            return DigitalOutputClear;
        }

        /// <summary>
        /// Creates a message that clears the specified digital output lines.
        /// </summary>
        /// <param name="messageType">Specifies the type of the created message.</param>
        /// <returns>A new message for the DigitalOutputClear register.</returns>
        public HarpMessage GetMessage(MessageType messageType)
        {
            return Harp.Hobgoblin.DigitalOutputClear.FromPayload(messageType, GetPayload());
        }
    }

    /// <summary>
    /// Represents an operator that creates a timestamped message payload
    /// that clears the specified digital output lines.
    /// </summary>
    [DisplayName("TimestampedDigitalOutputClearPayload")]
    [Description("Creates a timestamped message payload that clears the specified digital output lines.")]
    public partial class CreateTimestampedDigitalOutputClearPayload : CreateDigitalOutputClearPayload
    {
        /// <summary>
        /// Creates a timestamped message that clears the specified digital output lines.
        /// </summary>
        /// <param name="timestamp">The timestamp of the message payload, in seconds.</param>
        /// <param name="messageType">Specifies the type of the created message.</param>
        /// <returns>A new timestamped message for the DigitalOutputClear register.</returns>
        public HarpMessage GetMessage(double timestamp, MessageType messageType)
        {
            return Harp.Hobgoblin.DigitalOutputClear.FromPayload(timestamp, messageType, GetPayload());
        }
    }

    /// <summary>
    /// Represents an operator that creates a message payload
    /// that toggles the specified digital output lines.
    /// </summary>
    [DisplayName("DigitalOutputTogglePayload")]
    [Description("Creates a message payload that toggles the specified digital output lines.")]
    public partial class CreateDigitalOutputTogglePayload
    {
        /// <summary>
        /// Gets or sets the value that toggles the specified digital output lines.
        /// </summary>
        [Description("The value that toggles the specified digital output lines.")]
        public DigitalOutputs DigitalOutputToggle { get; set; }

        /// <summary>
        /// Creates a message payload for the DigitalOutputToggle register.
        /// </summary>
        /// <returns>The created message payload value.</returns>
        public DigitalOutputs GetPayload()
        {
            return DigitalOutputToggle;
        }

        /// <summary>
        /// Creates a message that toggles the specified digital output lines.
        /// </summary>
        /// <param name="messageType">Specifies the type of the created message.</param>
        /// <returns>A new message for the DigitalOutputToggle register.</returns>
        public HarpMessage GetMessage(MessageType messageType)
        {
            return Harp.Hobgoblin.DigitalOutputToggle.FromPayload(messageType, GetPayload());
        }
    }

    /// <summary>
    /// Represents an operator that creates a timestamped message payload
    /// that toggles the specified digital output lines.
    /// </summary>
    [DisplayName("TimestampedDigitalOutputTogglePayload")]
    [Description("Creates a timestamped message payload that toggles the specified digital output lines.")]
    public partial class CreateTimestampedDigitalOutputTogglePayload : CreateDigitalOutputTogglePayload
    {
        /// <summary>
        /// Creates a timestamped message that toggles the specified digital output lines.
        /// </summary>
        /// <param name="timestamp">The timestamp of the message payload, in seconds.</param>
        /// <param name="messageType">Specifies the type of the created message.</param>
        /// <returns>A new timestamped message for the DigitalOutputToggle register.</returns>
        public HarpMessage GetMessage(double timestamp, MessageType messageType)
        {
            return Harp.Hobgoblin.DigitalOutputToggle.FromPayload(timestamp, messageType, GetPayload());
        }
    }

    /// <summary>
    /// Represents an operator that creates a message payload
    /// that writes the state of all digital output lines.
    /// </summary>
    [DisplayName("DigitalOutputStatePayload")]
    [Description("Creates a message payload that writes the state of all digital output lines.")]
    public partial class CreateDigitalOutputStatePayload
    {
        /// <summary>
        /// Gets or sets the value that writes the state of all digital output lines.
        /// </summary>
        [Description("The value that writes the state of all digital output lines.")]
        public DigitalOutputs DigitalOutputState { get; set; }

        /// <summary>
        /// Creates a message payload for the DigitalOutputState register.
        /// </summary>
        /// <returns>The created message payload value.</returns>
        public DigitalOutputs GetPayload()
        {
            return DigitalOutputState;
        }

        /// <summary>
        /// Creates a message that writes the state of all digital output lines.
        /// </summary>
        /// <param name="messageType">Specifies the type of the created message.</param>
        /// <returns>A new message for the DigitalOutputState register.</returns>
        public HarpMessage GetMessage(MessageType messageType)
        {
            return Harp.Hobgoblin.DigitalOutputState.FromPayload(messageType, GetPayload());
        }
    }

    /// <summary>
    /// Represents an operator that creates a timestamped message payload
    /// that writes the state of all digital output lines.
    /// </summary>
    [DisplayName("TimestampedDigitalOutputStatePayload")]
    [Description("Creates a timestamped message payload that writes the state of all digital output lines.")]
    public partial class CreateTimestampedDigitalOutputStatePayload : CreateDigitalOutputStatePayload
    {
        /// <summary>
        /// Creates a timestamped message that writes the state of all digital output lines.
        /// </summary>
        /// <param name="timestamp">The timestamp of the message payload, in seconds.</param>
        /// <param name="messageType">Specifies the type of the created message.</param>
        /// <returns>A new timestamped message for the DigitalOutputState register.</returns>
        public HarpMessage GetMessage(double timestamp, MessageType messageType)
        {
            return Harp.Hobgoblin.DigitalOutputState.FromPayload(timestamp, messageType, GetPayload());
        }
    }

    /// <summary>
    /// Represents an operator that creates a message payload
    /// that starts a pulse train driving the specified digital output lines.
    /// </summary>
    [DisplayName("StartPulseTrainPayload")]
    [Description("Creates a message payload that starts a pulse train driving the specified digital output lines.")]
    public partial class CreateStartPulseTrainPayload
    {
        /// <summary>
        /// Gets or sets a value that specifies the digital output lines set by each pulse of the pulse train.
        /// </summary>
        [Description("Specifies the digital output lines set by each pulse of the pulse train.")]
        public DigitalOutputs DigitalOutput { get; set; }

        /// <summary>
        /// Gets or sets a value that specifies the duration in microseconds that each pulse is HIGH.
        /// </summary>
        [Description("Specifies the duration in microseconds that each pulse is HIGH.")]
        public uint PulseWidth { get; set; } = 500000;

        /// <summary>
        /// Gets or sets a value that specifies the interval in microseconds between each pulse in the pulse train.
        /// </summary>
        [Description("Specifies the interval in microseconds between each pulse in the pulse train.")]
        public uint PulsePeriod { get; set; } = 1000000;

        /// <summary>
        /// Gets or sets a value that specifies the number of pulses in the PWM pulse train. A value of zero signifies an infinite pulse train.
        /// </summary>
        [Description("Specifies the number of pulses in the PWM pulse train. A value of zero signifies an infinite pulse train.")]
        public uint PulseCount { get; set; } = 1;

        /// <summary>
        /// Creates a message payload for the StartPulseTrain register.
        /// </summary>
        /// <returns>The created message payload value.</returns>
        public StartPulseTrainPayload GetPayload()
        {
            StartPulseTrainPayload value;
            value.DigitalOutput = DigitalOutput;
            value.PulseWidth = PulseWidth;
            value.PulsePeriod = PulsePeriod;
            value.PulseCount = PulseCount;
            return value;
        }

        /// <summary>
        /// Creates a message that starts a pulse train driving the specified digital output lines.
        /// </summary>
        /// <param name="messageType">Specifies the type of the created message.</param>
        /// <returns>A new message for the StartPulseTrain register.</returns>
        public HarpMessage GetMessage(MessageType messageType)
        {
            return Harp.Hobgoblin.StartPulseTrain.FromPayload(messageType, GetPayload());
        }
    }

    /// <summary>
    /// Represents an operator that creates a timestamped message payload
    /// that starts a pulse train driving the specified digital output lines.
    /// </summary>
    [DisplayName("TimestampedStartPulseTrainPayload")]
    [Description("Creates a timestamped message payload that starts a pulse train driving the specified digital output lines.")]
    public partial class CreateTimestampedStartPulseTrainPayload : CreateStartPulseTrainPayload
    {
        /// <summary>
        /// Creates a timestamped message that starts a pulse train driving the specified digital output lines.
        /// </summary>
        /// <param name="timestamp">The timestamp of the message payload, in seconds.</param>
        /// <param name="messageType">Specifies the type of the created message.</param>
        /// <returns>A new timestamped message for the StartPulseTrain register.</returns>
        public HarpMessage GetMessage(double timestamp, MessageType messageType)
        {
            return Harp.Hobgoblin.StartPulseTrain.FromPayload(timestamp, messageType, GetPayload());
        }
    }

    /// <summary>
    /// Represents an operator that creates a message payload
    /// that stops the pulse train running on the specified digital output lines.
    /// </summary>
    [DisplayName("StopPulseTrainPayload")]
    [Description("Creates a message payload that stops the pulse train running on the specified digital output lines.")]
    public partial class CreateStopPulseTrainPayload
    {
        /// <summary>
        /// Gets or sets the value that stops the pulse train running on the specified digital output lines.
        /// </summary>
        [Description("The value that stops the pulse train running on the specified digital output lines.")]
        public DigitalOutputs StopPulseTrain { get; set; }

        /// <summary>
        /// Creates a message payload for the StopPulseTrain register.
        /// </summary>
        /// <returns>The created message payload value.</returns>
        public DigitalOutputs GetPayload()
        {
            return StopPulseTrain;
        }

        /// <summary>
        /// Creates a message that stops the pulse train running on the specified digital output lines.
        /// </summary>
        /// <param name="messageType">Specifies the type of the created message.</param>
        /// <returns>A new message for the StopPulseTrain register.</returns>
        public HarpMessage GetMessage(MessageType messageType)
        {
            return Harp.Hobgoblin.StopPulseTrain.FromPayload(messageType, GetPayload());
        }
    }

    /// <summary>
    /// Represents an operator that creates a timestamped message payload
    /// that stops the pulse train running on the specified digital output lines.
    /// </summary>
    [DisplayName("TimestampedStopPulseTrainPayload")]
    [Description("Creates a timestamped message payload that stops the pulse train running on the specified digital output lines.")]
    public partial class CreateTimestampedStopPulseTrainPayload : CreateStopPulseTrainPayload
    {
        /// <summary>
        /// Creates a timestamped message that stops the pulse train running on the specified digital output lines.
        /// </summary>
        /// <param name="timestamp">The timestamp of the message payload, in seconds.</param>
        /// <param name="messageType">Specifies the type of the created message.</param>
        /// <returns>A new timestamped message for the StopPulseTrain register.</returns>
        public HarpMessage GetMessage(double timestamp, MessageType messageType)
        {
            return Harp.Hobgoblin.StopPulseTrain.FromPayload(timestamp, messageType, GetPayload());
        }
    }

    /// <summary>
    /// Represents an operator that creates a message payload
    /// that reports the sampled analog signal on each of the ADC input channels.
    /// </summary>
    [DisplayName("AnalogDataPayload")]
    [Description("Creates a message payload that reports the sampled analog signal on each of the ADC input channels.")]
    public partial class CreateAnalogDataPayload
    {
        /// <summary>
        /// Gets or sets a value that the analog value sampled from ADC channel 0.
        /// </summary>
        [Description("The analog value sampled from ADC channel 0.")]
        public byte AnalogInput0 { get; set; }

        /// <summary>
        /// Gets or sets a value that the analog value sampled from ADC channel 1.
        /// </summary>
        [Description("The analog value sampled from ADC channel 1.")]
        public byte AnalogInput1 { get; set; }

        /// <summary>
        /// Gets or sets a value that the analog value sampled from ADC channel 2.
        /// </summary>
        [Description("The analog value sampled from ADC channel 2.")]
        public byte AnalogInput2 { get; set; }

        /// <summary>
        /// Creates a message payload for the AnalogData register.
        /// </summary>
        /// <returns>The created message payload value.</returns>
        public AnalogDataPayload GetPayload()
        {
            AnalogDataPayload value;
            value.AnalogInput0 = AnalogInput0;
            value.AnalogInput1 = AnalogInput1;
            value.AnalogInput2 = AnalogInput2;
            return value;
        }

        /// <summary>
        /// Creates a message that reports the sampled analog signal on each of the ADC input channels.
        /// </summary>
        /// <param name="messageType">Specifies the type of the created message.</param>
        /// <returns>A new message for the AnalogData register.</returns>
        public HarpMessage GetMessage(MessageType messageType)
        {
            return Harp.Hobgoblin.AnalogData.FromPayload(messageType, GetPayload());
        }
    }

    /// <summary>
    /// Represents an operator that creates a timestamped message payload
    /// that reports the sampled analog signal on each of the ADC input channels.
    /// </summary>
    [DisplayName("TimestampedAnalogDataPayload")]
    [Description("Creates a timestamped message payload that reports the sampled analog signal on each of the ADC input channels.")]
    public partial class CreateTimestampedAnalogDataPayload : CreateAnalogDataPayload
    {
        /// <summary>
        /// Creates a timestamped message that reports the sampled analog signal on each of the ADC input channels.
        /// </summary>
        /// <param name="timestamp">The timestamp of the message payload, in seconds.</param>
        /// <param name="messageType">Specifies the type of the created message.</param>
        /// <returns>A new timestamped message for the AnalogData register.</returns>
        public HarpMessage GetMessage(double timestamp, MessageType messageType)
        {
            return Harp.Hobgoblin.AnalogData.FromPayload(timestamp, messageType, GetPayload());
        }
    }

    /// <summary>
    /// Represents the payload of the StartPulseTrain register.
    /// </summary>
    public struct StartPulseTrainPayload
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StartPulseTrainPayload"/> structure.
        /// </summary>
        /// <param name="digitalOutput">Specifies the digital output lines set by each pulse of the pulse train.</param>
        /// <param name="pulseWidth">Specifies the duration in microseconds that each pulse is HIGH.</param>
        /// <param name="pulsePeriod">Specifies the interval in microseconds between each pulse in the pulse train.</param>
        /// <param name="pulseCount">Specifies the number of pulses in the PWM pulse train. A value of zero signifies an infinite pulse train.</param>
        public StartPulseTrainPayload(
            DigitalOutputs digitalOutput,
            uint pulseWidth,
            uint pulsePeriod,
            uint pulseCount)
        {
            DigitalOutput = digitalOutput;
            PulseWidth = pulseWidth;
            PulsePeriod = pulsePeriod;
            PulseCount = pulseCount;
        }

        /// <summary>
        /// Specifies the digital output lines set by each pulse of the pulse train.
        /// </summary>
        public DigitalOutputs DigitalOutput;

        /// <summary>
        /// Specifies the duration in microseconds that each pulse is HIGH.
        /// </summary>
        public uint PulseWidth;

        /// <summary>
        /// Specifies the interval in microseconds between each pulse in the pulse train.
        /// </summary>
        public uint PulsePeriod;

        /// <summary>
        /// Specifies the number of pulses in the PWM pulse train. A value of zero signifies an infinite pulse train.
        /// </summary>
        public uint PulseCount;

        /// <summary>
        /// Returns a <see cref="string"/> that represents the payload of
        /// the StartPulseTrain register.
        /// </summary>
        /// <returns>
        /// A <see cref="string"/> that represents the payload of the
        /// StartPulseTrain register.
        /// </returns>
        public override string ToString()
        {
            return "StartPulseTrainPayload { " +
                "DigitalOutput = " + DigitalOutput + ", " +
                "PulseWidth = " + PulseWidth + ", " +
                "PulsePeriod = " + PulsePeriod + ", " +
                "PulseCount = " + PulseCount + " " +
            "}";
        }
    }

    /// <summary>
    /// Represents the payload of the AnalogData register.
    /// </summary>
    public struct AnalogDataPayload
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AnalogDataPayload"/> structure.
        /// </summary>
        /// <param name="analogInput0">The analog value sampled from ADC channel 0.</param>
        /// <param name="analogInput1">The analog value sampled from ADC channel 1.</param>
        /// <param name="analogInput2">The analog value sampled from ADC channel 2.</param>
        public AnalogDataPayload(
            byte analogInput0,
            byte analogInput1,
            byte analogInput2)
        {
            AnalogInput0 = analogInput0;
            AnalogInput1 = analogInput1;
            AnalogInput2 = analogInput2;
        }

        /// <summary>
        /// The analog value sampled from ADC channel 0.
        /// </summary>
        public byte AnalogInput0;

        /// <summary>
        /// The analog value sampled from ADC channel 1.
        /// </summary>
        public byte AnalogInput1;

        /// <summary>
        /// The analog value sampled from ADC channel 2.
        /// </summary>
        public byte AnalogInput2;

        /// <summary>
        /// Returns a <see cref="string"/> that represents the payload of
        /// the AnalogData register.
        /// </summary>
        /// <returns>
        /// A <see cref="string"/> that represents the payload of the
        /// AnalogData register.
        /// </returns>
        public override string ToString()
        {
            return "AnalogDataPayload { " +
                "AnalogInput0 = " + AnalogInput0 + ", " +
                "AnalogInput1 = " + AnalogInput1 + ", " +
                "AnalogInput2 = " + AnalogInput2 + " " +
            "}";
        }
    }

    /// <summary>
    /// Specifies the state of port digital input lines.
    /// </summary>
    [Flags]
    public enum DigitalInputs : byte
    {
        None = 0x0,
        GP2 = 0x1,
        GP3 = 0x2,
        GP12 = 0x4,
        GP13 = 0x8,
        GP14 = 0x10
    }

    /// <summary>
    /// Specifies the state of port digital output lines.
    /// </summary>
    [Flags]
    public enum DigitalOutputs : byte
    {
        None = 0x0,
        GP15 = 0x1,
        GP16 = 0x2,
        GP17 = 0x4,
        GP18 = 0x8,
        GP19 = 0x10,
        GP20 = 0x20,
        GP21 = 0x40,
        GP22 = 0x80
    }
}
