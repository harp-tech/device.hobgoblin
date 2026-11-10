# Reaction time task

In this tutorial, you will learn how to use the Hobgoblin to implement a reaction time task which can be easily adapted and modified to model a wide range of systems neuroscience experiments.

When designing operant behavior assays, it is useful to describe the task as a sequence of states the system goes through (e.g. stimulus on, stimulus off, reward, inter-trial interval, etc). Progression through these states is driven by events, which can be either internal or external to the system (e.g. button press, timeout, stimulus offset, movement onset). It is common to describe the interplay between states and events in the form of a finite-state machine diagram, or graph, where nodes are states, and arrows are events.

A reaction time task, where the subject needs to press a button as fast as possible following a stimulus, is described in the following diagram:

```mermaid
stateDiagram-v2
    direction LR
    [*] --> ITI
    ITI --> ON: elapsed
    ON --> Reward: hit
    ON --> Fail: miss
    Reward --> [*]
    Fail --> [*]
```

The task begins with an inter-trial interval (`ITI`), followed by stimulus presentation (`ON`). After stimulus onset, advancement to the next state can happen only when the subject presses the button (`success`) or a timeout elapses (`miss`). Depending on which event is triggered first, the task advances either to the `Reward` state, or `Fail` state. At the end, the task goes back to the beginning of the ITI state for the next trial.

## Prerequisites

- Install Bonsai and the `Harp.Hobgoblin` package, as described in [Installation](installation.md).
- Install the [`Bonsai.Numerics`](https://bonsai-rx.org/numerics/) package from the [Bonsai package manager](https://bonsai-rx.org/docs/articles/packages.html).

### Exercise 1: Generating a fixed-interval stimulus

In this first exercise, you will assemble the basic hardware and software components required to implement the reaction time task. Connect the LED to digital output channel `GP15` on the `Hobgoblin`. Connect the push button to digital input channel `GP2` on the `Hobgoblin`. 

>[!TIP]
> You can use other digital input or digital output channels, but make sure to change the appropriate properties.

![Hobgoblin RTT](../images/hobgoblin-reactiontime-ledbutton.svg){width=520px}

Next, we will set up our `Hobgoblin`. 

:::workflow
![Hobgoblin Device Pattern](../workflows/hobgoblin-reactiontime-devicepattern.bonsai)
:::

- Insert a [`Device`] operator and set the `PortName` property.
- Insert a [`DeviceDataWriter`] and set the `Path` property. Connecting it directly to the device ensures thats all events are logged.
- During this tutorial we will need to have the ability to send/receive commands from distinct places in the workflow. To allow this kind of "many-to-one"/"one-to-many" communication, we will:
    - Insert a [`PublishSubject`] operator and name it `Hobgoblin Events`. This operator broadcasts events from the `Hobgoblin`, which you can receive at multiple points in the workflow using a [`SubscribeSubject`].
    - Right-click the [`Device`] operator, select `Create Source (Bonsai.Harp.HarpMessage)` > [`BehaviorSubject`]. Name the generated ``BehaviorSubject`1`` operator `Hobgoblin Commands`. Connect it as input to the [`Device`] operator.

>[!NOTE]
> A [source subject](https://bonsai-rx.org/docs/articles/subjects.html#source-subjects) is an operator that is set up to receive commands from multiple places in the workflow and pass them on to the `Hobgoblin`. When choosing a type for the `source subject`, choosing a [`BehaviorSubject`] instead of a [`PublishSubject`] ensures that the connection remains open until the workflow is stopped.

> [!NOTE]
> We will use this `device pattern` when setting up our `Hobgoblin` for the rest of the tutorials.

Lastly, we will set up a fixed-interval blinking LED as our stimulus.

:::workflow
![Hobgoblin Reaction Time Stimulus](../workflows/hobgoblin-reactiontime-stimulus.bonsai)
:::

- Insert a [`Timer`] source and set its `DueTime` property to 1 second.
- Insert a [`CreateMessage`] operator, configure the `Payload` property to [`DigitalOutputSetPayload`], and set the [`DigitalOutputSet`] property to the `GP15` pin.
- Insert a [`MulticastSubject`] operator and configure the `Name` property to `Hobgoblin Commands`. This operator forwards `HarpMessages` it receives to `Hobgoblin Commands`.
- Insert a [`Delay`] operator and set its `DueTime` property to 200 milliseconds.
- Insert a [`CreateMessage`] operator, configure the `Payload` property to [`DigitalOutputClearPayload`], and set the [`DigitalOutputClear`] property to the `GP15` pin.
- Insert a [`MulticastSubject`] operator and configure the `Name` property to `Hobgoblin Commands`.
- Insert a [`Repeat`] operator.
- Run the workflow, and verify that the LED is blinking.

### Exercise 2: Measuring reaction time

:::workflow
![Hobgoblin Reaction Time Measurement](../workflows/hobgoblin-reactiontime-measurement.bonsai)
:::

- Insert a [`SubscribeSubject`] operator. Configure the `Name` property to `Hobgoblin Events`.
- Insert a [`Parse`] operator after `Hobgoblin Events`. Configure the `Register` property to [`TimestampedDigitalOutputSet`].
- Insert a [`SubscribeSubject`] operator. Configure the `Name` property to `Hobgoblin Events`.
- Insert a [`Parse`] operator after `Hobgoblin Events`. Configure the `Register` property to [`TimestampedDigitalInputState`].
- Run the workflow, verify that both the stimulus and the button are correctly recorded.

>[!TIP]
> We use separate `Hobgoblin Events` operators to avoid issues with [branching](https://bonsai-rx.org/docs/articles/subjects.html#branching-subjects). See also [workflow guidelines](https://bonsai-rx.org/docs/articles/workflow-guidelines.html)

### Exercise 3: Analyzing reaction time

We will take a brief detour from Bonsai to look at how to visualize the data we have recorded. This section assumes you already have a Python environment with [`pandas`](https://pandas.pydata.org/), [`matplotlib`](https://matplotlib.org/), [`harp-data`](https://harp-tech.org/python/) and the `harp-hobgoblin` interface installed, as described in [Acquisition and control](acquisition-and-control.md#exercise-4-visualizing-recorded-data).

```python
import pandas as pd
from harp.data import open_dataset
from harp.device import hobgoblin

# Open the recorded dataset with the Hobgoblin register definitions
reader = open_dataset("./Hobgoblin.harp", hobgoblin)

# Load digital input and digital output, with one column per pin
digital_output_set = reader.read(hobgoblin.DigitalOutputSet, demux_bit_masks=True)
digital_input_state = reader.read(hobgoblin.DigitalInputState, demux_bit_masks=True)

# Inspect the data frames
print(digital_output_set.head())
print(digital_input_state.head())

# Keep only the pins used in the task
digital_output_set = digital_output_set["GP15"]
digital_input_state = digital_input_state["GP2"]

# Keep stimulus onsets and button presses
digital_output_set = digital_output_set[digital_output_set]
digital_input_state = digital_input_state[digital_input_state]

# Extract valid responses (first button press that occurs within response_window, our ITI is ~1.2 second)
response_window = 1.0
valid_response_times = []
for led_on in digital_output_set.index:
    for button_press in digital_input_state.index:
        response_time = button_press - led_on
        if 0 < response_time < response_window:
            valid_response_times.append(response_time)
            break

# Calculate and print hit/miss percentage
num_valid_responses = len(valid_response_times)
num_total_trials = len(digital_output_set.index)
hit_percentage = num_valid_responses / num_total_trials * 100
print(f"There were {num_valid_responses} valid responses out of {num_total_trials} trials, giving a hit rate of {hit_percentage}%")

# Plot valid response times
pd.Series(valid_response_times).plot(kind="hist")
```

### Exercise 4: Driving state transitions with external behavior events

In order to translate our simple reaction time task in the previous exercises into a proper state machine, we need to split up the fixed interval stimulus into different states. It is often convenient to consider the inter-trial interval period as the initial state, followed by stimulus presentation. 

:::workflow
![Stimulus presentation](../workflows/hobgoblin-reactiontime-stimulus-response.bonsai)
:::

- Copy the workflows from [Exercise 1](#exercise-1-generating-a-fixed-interval-stimulus).
- Select the [`Timer`] operator and set its `DueTime` property to 3 second.
- Click and drag to select both the [`CreateMessage`] ([`DigitalOutputSetPayload`]) and `Hobgoblin Commands` operators.
- Right-click, select `Group` > `Sink (Reactive)`. Set the `Name` property to `StimOn`.
- Click and drag to select both the [`CreateMessage`] ([`DigitalOutputClearPayload`]) and `Hobgoblin Commands` operators.
- Right-click, select `Group` > `Sink (Reactive)`. Set the `Name` property to `StimOff`.

> [!Note]
> The [`Sink`] operator allows you to specify arbitrary processing side-effects without affecting the original flow of events. It is often used to trigger and control stimulus presentation in response to events in the task. Inside the nested specification, `Source1` represents input events arriving at the sink. In the specific case of [`Sink`] operators, the `WorkflowOutput` node can be safely ignored.

- Delete the [`Delay`] operator.
- Insert a [`SelectMany`] operator after `StimOn`, and set its `Name` property to `Response`.
- Double-click on the [`SelectMany`] node to open up its internal specification.

> [!Note]
> The `SelectMany` operator is used here to create a new state for every input event. `Source1` represents the input event that created the state, and `WorkflowOutput` will be used to report the end result from the state (e.g. whether the response was a success or failure).

:::workflow
![Stimulus presentation](../workflows/hobgoblin-reactiontime-stimulus-response-input.bonsai)
:::

- Delete the `Source1` operator.
- Insert a [`SubscribeSubject`] operator. Configure the `Name` property to `Hobgoblin Events`.
- Insert a [`Parse`] operator after `Hobgoblin Events`. Configure the `Register` property to [`DigitalInputState`].
- Insert a [`Condition`] operator after the [`Parse`] operator. 
- Double-click on the [`Condition`] operator and add a [`HasFlag`] operator after the `Source1` operator. Set the `Value` property to `GP2`.
- Insert a [`Take`] operator and set its `Count` property to 1.
- Connect the [`Take`] operator to `WorkflowOutput`.
- Run the workflow a couple of times and validate the state machine is responding to the button press.

> [!Note]
> The [`Condition`] operator allows you to specify arbitrary rules for accepting or rejecting inputs. The `WorkflowOutput` node always needs to be specified with a `bool` input, the result of whether the input is accepted (`True`) or rejected (`False`). Only inputs which pass the filter specified inside the [`Condition`] are allowed to proceed. Using a [`HasFlag`] operator here allows us to filter only messages from that pin and also has the beneficial side effect of only detecting a button press (when the value changes fron `None` > `GP2`) instead of a button release (`GP2`>`None`).

### Exercise 5: Timeout and choice

We will modify our previous workflow to include a timeout during which the responder must make a choice.

**`Response`**:
:::workflow
![Stimulus presentation](../workflows/hobgoblin-reactiontime-stimulus-response-timeout.bonsai)
:::

- Inside the `Response` node, insert a [`Timer`] source and set its `DueTime` property to be about 1 second.
- Insert a [`Boolean`] operator and set the `Value` property to `False`
- Insert another [`Boolean`] operator after the [`Condition`] operator and set the `Value` property to `True`
- Join both [`Boolean`] operators with a [`Merge`] combinator.
- Run the workflow a couple of times, opening the visualizer of the `Response` node.

_Describe in your own words what the above modified workflow is doing._

### Exercise 6: Specifying conditional task outcomes

We will now modify our workflow to set up a transition to the next state.

:::workflow
![Stimulus response outcomes](../workflows/hobgoblin-reactiontime-stimulus-response-outcomes.bonsai)
:::

- Insert a [`Condition`] operator after the `StimOff` node, and set its `Name` property to `Success`.
- In a new branch from `StimOff`, insert another [`Condition`], and set its `Name` property to `Miss`.
- Insert a [`SelectMany`] operator after the `Success` condition and change its `Name` property to `Reward`.
- Insert a [`SelectMany`] operator after the `Miss` condition and change its `Name` property to `Fail`.

We will now implement the task logic inside our nodes.

**`Miss`**:
:::workflow
![Stimulus response miss condition](../workflows/hobgoblin-reactiontime-stimulus-response-miss-condition.bonsai)
:::

- Double-click on the `Miss` node to open up its internal specification.
- Insert a [`BitwiseNot`] operator after `Source1`.

_Why did we need to specify something for the `Miss` condition?_
_Why did we not need to specify anything for the `Success` condition?_

> [!Note]
> The [`Condition`] operator is often used to represent choice points in the task. You can use operators such as [`Equal`], [`NotEqual`], [`GreaterThan`], etc for specifying such tests.

Inside the `Reward` and `Fail` node you can specify your own logic to signal the state of the trial. 
For example, to make the LED blink three times in rapid succession for the `Reward` node:

**`Reward`**: 
:::workflow
![Stimulus response miss condition](../workflows/hobgoblin-reactiontime-stimulus-response-outcomes-reward.bonsai)
:::

- Delete the `Source1` operator.
- Copy the fixed-interval blinking LED from [Exercise 1](#exercise-1-generating-a-fixed-interval-stimulus).
- In the [`Timer`] node, set both the `DueTime` and the `Period` properties to 100ms. 
- In the [`Delay`] node, set the `DueTime` properties to 100ms.
- Delete the [`Repeat`] node.
- Insert a [`RepeatCount`], set the `Count` property to 3.
- Insert the [`Last`] operator, and connect it to `WorkflowOutput`.

_Try out your state machine and check whether you understand the behavior of the reward signal._

- Copy the workflow in the `Reward` node and paste it into the `Miss` condition.
- **Optional**: Modify the `Fail` state in some way to signal a different trial outcome (e.g. make the LED blink more times, or move a motor).

- In the top-level workflow, insert a [`Merge`] operator and connect to it the outputs of both conditional branches and before the [`Repeat`] node.

_Try out your state machine and introduce variations to the task behavior and conditions._

### Exercise 7: Go/No-Go task

Implement the following trial structure for a Go/No-Go task.

```mermaid
stateDiagram-v2
    direction LR
    NoGo: No-Go
    FalseAlarm: False<br>Alarm
    CorrectReject: Correct<br>Reject
    [*] --> ITI
    ITI --> Go: 50%
    ITI --> NoGo: 50%
    Go --> Hit
    Go --> Miss
    NoGo --> FalseAlarm
    NoGo --> CorrectReject
```

- Trials should be sampled from a uniform distribution using the `Bonsai.Numerics` package.
- Response events should be based on a button press, and reject events on a timeout.
- Make sure to implement different visual or auditory feedback for either the cue or reward/failure states.

> [!Tip]
> To sample values from a discrete uniform distribution, you can use the following workflow: 
> :::workflow
> ![Sampling Values](../workflows/hobgoblin-reactiontime-samplediscreteuniform.bonsai)
> :::

- Record a timestamped chronological log of trial types and rewards into a CSV file using a [`BehaviorSubject`].

<!--Reference Style Links -->
[`BehaviorSubject`]: xref:Bonsai.Reactive.BehaviorSubject
[`BitwiseNot`]: xref:Bonsai.Expressions.BitwiseNotBuilder
[`Boolean`]: xref:Bonsai.Expressions.BooleanProperty
[`Condition`]: xref:Bonsai.Reactive.Condition
[`CreateMessage`]: xref:Harp.Hobgoblin.CreateMessage
[`Delay`]: xref:Bonsai.Reactive.Delay
[`Device`]: xref:Harp.Hobgoblin.Device
[`DeviceDataWriter`]: xref:Harp.Hobgoblin.DeviceDataWriter
[`DigitalInputState`]: xref:Harp.Hobgoblin.DigitalInputState
[`DigitalOutputSet`]: xref:Harp.Hobgoblin.DigitalOutputSet
[`DigitalOutputClear`]: xref:Harp.Hobgoblin.DigitalOutputClear
[`DigitalOutputSetPayload`]: xref:Harp.Hobgoblin.CreateDigitalOutputSetPayload
[`DigitalOutputClearPayload`]: xref:Harp.Hobgoblin.CreateDigitalOutputClearPayload
[`Equal`]: xref:Bonsai.Expressions.EqualBuilder
[`GreaterThan`]: xref:Bonsai.Expressions.GreaterThanBuilder
[`HarpMessage`]: xref:Bonsai.Harp.HarpMessage
[`HasFlag`]: xref:Bonsai.Expressions.HasFlagBuilder
[`Last`]: xref:Bonsai.Reactive.Last
[`Merge`]: xref:Bonsai.Reactive.Merge
[`MulticastSubject`]: xref:Bonsai.Expressions.MulticastSubject
[`NotEqual`]: xref:Bonsai.Expressions.NotEqualBuilder
[`Parse`]: xref:Harp.Hobgoblin.Parse
[`PublishSubject`]: xref:Bonsai.Reactive.PublishSubject
[`Repeat`]: xref:Bonsai.Reactive.Repeat
[`RepeatCount`]: xref:Bonsai.Reactive.RepeatCount
[`SelectMany`]: xref:Bonsai.Reactive.SelectMany
[`Sink`]: xref:Bonsai.Reactive.Sink
[`SubscribeSubject`]: xref:Bonsai.Expressions.SubscribeSubject
[`Take`]: xref:Bonsai.Reactive.Take
[`Timer`]: xref:Bonsai.Reactive.Timer
[`TimestampedDigitalOutputSet`]: xref:Harp.Hobgoblin.TimestampedDigitalOutputSet
[`TimestampedDigitalInputState`]: xref:Harp.Hobgoblin.TimestampedDigitalInputState