# Closed-loop systems

The exercises below will help you become familiar with using the Hobgoblin for closed-loop experiments. You will also learn how to interface with external cameras. Before you begin, it is recommended that you review the Bonsai [Acquisition and Tracking](https://bonsai-rx.org/docs/tutorials/acquisition.html) tutorial, which covers key video concepts.

## Prerequisites

- Install Bonsai and the `Harp.Hobgoblin` package, as described in [Installation](installation.md).
- Install the `Bonsai.Dsp`, `Bonsai.Video`, `Bonsai.Vision`, and `Bonsai.Vision.Design` packages from the [Bonsai package manager](https://bonsai-rx.org/docs/articles/packages.html).

## Closed-loop latency

In a closed-loop experiment, we want the behavior data to generate feedback in real time into the external world, establishing a relationship where the output of the system depends on detected sensory input. Many behavioral experiments in neuroscience require some kind of closed-loop interaction between the subject and the experimental setup.

One of the most important benchmarks to evaluate the performance of a closed-loop system is the latency, or the time it takes for a change in the output to be generated in response to a change in the input. To measure the latency of a closed-loop system, you can use a digital feedback loop.

Before beginning, set up the `Hobgoblin` with the following `device pattern` that we learned about in the [Reaction time task](reaction-time-task.md#exercise-1-generating-a-fixed-interval-stimulus) tutorial.

:::workflow
![Hobgoblin Device Pattern](../workflows/hobgoblin-closeloop-devicepattern.bonsai)
:::

- Check that the `DumpRegisters` property in the `Hobgoblin` [`Device`] operator is set to `False`. This is to avoid triggering the command loop in the next exercise.

### Exercise 1: Measuring serial port communication latency

We will take advantage of the device's ability to echo back a timestamped message upon command execution to create a simple loop where each echo re-triggers the same command (toggling the digital output channel `HIGH` and `LOW`). The time interval between the echoes will give us the total closed-loop latency of the system, also known as the round-trip time.

:::workflow
![Hobgoblin Closed-Loop Latency](../workflows/hobgoblin-closeloop-latency.bonsai)
:::

- Insert a [`SubscribeSubject`] operator and configure the `Name` property to `Hobgoblin Events`.
- Insert a [`Parse`] operator and select [`TimestampedDigitalOutputToggle`] from the `Register` property dropdown menu.
- Insert a [`CreateMessage`] operator, select [`DigitalOutputTogglePayload`] for the `Payload`, and `GP15` for the [`DigitalOutputToggle`] property.
- Insert a [`MulticastSubject`] operator and configure the `Name` property to `Hobgoblin Commands`.

To measure the difference between the timestamps:

- Hold down the <kbd>Alt</kbd> key, right-click the [`Parse`] operator, select `Output (Bonsai.Harp.Timestamped<Harp.Hobgoblin.DigitalOutputs>)` > `Seconds` from the context menu. The `Seconds` output should appear on a new branch.
- After the `Seconds` node, insert a [`Difference`] operator.

Lastly, we will use this sequence to toggle the digital output and initialize the command loop:

- Insert a [`KeyDown`] operator and set the `Filter` property to the key `A`.
- Insert a [`CreateMessage`] operator, select [`DigitalOutputTogglePayload`] for the `Payload`, and `GP15` for the [`DigitalOutputToggle`] property.
- Insert a [`MulticastSubject`] operator and configure the `Name` property to `Hobgoblin Commands`.
- Run the workflow, press the `A` key to start the command loop, and open the visualizer for the [`Difference`] operator.  
**What do you observe?**

### Exercise 2: Measuring video acquisition latency

![Hobgoblin LED](../images/hobgoblin-acquisition-led.svg){width=400px}

- Connect a red LED module to digital output channel `GP15` on the `Hobgoblin`.

:::workflow
![Hobgoblin Closed-Loop Latency Video](../workflows/hobgoblin-closeloop-latency-video.bonsai)
:::

- Insert a [`VideoCaptureDevice`] operator. Set the `Index` property to the right camera.
- Insert a [`Crop`] transform.
- Run the workflow and set the `RegionOfInterest` property to a small area around the LED.

> [!Tip]
> You can use the visual editor to set the region. While the workflow is running, right-click the [`Crop`] transform and select `Show Default Editor` from the context menu, or click the ellipsis button that appears when you select the `RegionOfInterest` property.

- Insert a [`Sum`] transform and select the `Val2` field from the output.

> [!Note]
> The [`Sum`] operator adds the value of all the pixels in the image together, across all the color channels. Assuming the default BGR format, the result of summing all the pixels in the `Red` channel of the image will be stored in `Val2`. `Val0` and `Val1` would store the `Blue` and `Green` values, respectively. If you are using an LED with a color other than red, please select the output field accordingly.

- Insert a [`GreaterThan`] transform.
- Insert a [`BitwiseNot`] transform.
- Insert a [`CreateMessage`] operator, select [`DigitalOutputTogglePayload`] for the `Payload`, and `GP15` for the [`DigitalOutputToggle`] property.
- Insert a [`MulticastSubject`] operator and configure the `Name` property to `Hobgoblin Commands`.
- Run the workflow and use the visualizer of the [`Sum`] operator to choose an appropriate threshold for [`GreaterThan`]. You can use the [`KeyDown`] toggle snippet from the previous exercise to manually toggle the LED.
- Insert a [`DistinctUntilChanged`] operator after the [`BitwiseNot`] transform.

> [!Note]
> The [`DistinctUntilChanged`] operator filters consecutive duplicate items from an observable sequence. In this case, we want to change the value of the LED only when the threshold output changes from `LOW` to `HIGH`, or vice-versa. This will let us measure correctly the latency between detecting a change in the input and measuring the response to that change.

To measure the round-trip time between the LED toggle:

:::workflow
![Hobgoblin Closed-Loop Latency Video Measurement](../workflows/hobgoblin-closeloop-latency-video-measurement.bonsai)
:::

- Insert a [`SubscribeSubject`] operator and configure the `Name` property to `Hobgoblin Events`.
- Insert a [`Parse`] operator and select [`TimestampedDigitalOutputToggle`] from the `Register` property dropdown menu.
- Right-click the [`Parse`] operator, select `Output (Bonsai.Harp.Timestamped<Harp.Hobgoblin.DigitalOutputs>)` > `Seconds` from the context menu.
- Insert a [`Difference`] operator.
- Run the workflow and open the visualizer for the [`Difference`] operator.

_Given the measurements obtained in Exercise 1 and Exercise 2, what would you estimate is the **input** latency for video acquisition?_

## Closed-loop control

### Exercise 3: Introduction to pulse trains

In neuroscience, pulse trains are commonly used to deliver precisely timed sequences of electrical states (`LOW` and `HIGH`, otherwise known as transistor-transistor logic or `TTL`) to control external devices, such as cameras for synchronization or lasers for optogenetic stimulation. However, due to operating system limitations, generating pulse trains in software like Bonsai can be prone to timing jitter (though this approach is sufficient for the stimuli we have been working with). Fortunately the `Hobgoblin` provides a dedicated register for each digital output (e.g. [`PulseTrainGP18`] for `GP18`) that can be used to start or stop hardware-programmed pulse trains. We will use `GP18`, one of the outputs whose pulse trains are generated by the PWM hardware of the Pico (see [Pulse trains](device.md#pulse-trains)).

:::workflow
![Hobgoblin Pulse Train](../workflows/hobgoblin-closeloop-pulsetrain.bonsai)
:::

- Connect an LED module to digital output channel `GP18` on the `Hobgoblin`.
- Insert a [`KeyDown`] operator and set the `Filter` property to the key `A`.
- Insert a [`CreateMessage`] operator and select [`PulseTrainGP18Payload`] for the `Payload`.
- Set the `PulseCount` property to `0`, `PulsePeriod` to `50000` and `PulseWidth` to `5000`. These parameters correspond to a continuous 20 Hz pulse train with 5 ms pulses.
- Insert a [`MulticastSubject`] operator and configure the `Name` property to `Hobgoblin Commands`.

In a separate branch:

- Insert another [`KeyDown`] operator and set the `Filter` property to the key `S`.
- Insert a [`CreateMessage`] operator, select [`DigitalOutputClearPayload`] for the `Payload`, and `GP18` for the [`DigitalOutputClear`] property. Clearing a digital output stops the pulse train. Setting the `PulseWidth` property of [`PulseTrainGP18Payload`] to `0` has the same effect.
- Insert a [`MulticastSubject`] operator and configure the `Name` property to `Hobgoblin Commands`.
- Run the workflow, use the `A` and `S` keys to start and stop the pulse train.  
**What do you observe?**

To better understand what each parameter controls, try the following modifications. Reset the values to the parameters above after each step.

- Increase the `PulsePeriod` to `200000`. What is the frequency of this stimulation? How would you increase the frequency of the pulses to 40 Hz?
- Increase the `PulseWidth` to `40000`. What do you observe?
- How would you deliver a 2 second pulse train? (Hint: Use `PulseCount`)

By default, the `Hobgoblin` reports the start of each pulse as a [`DigitalOutputSet`] event, so every pulse in the train gets a hardware timestamp. The [`PulseEventEnable`] register selects which digital outputs report these events.

:::workflow
![Hobgoblin Pulse Event Enable](../workflows/hobgoblin-closeloop-pulseeventenable.bonsai)
:::

- Insert a [`SubscribeSubject`] operator and configure the `Name` property to `Hobgoblin Events`.
- Insert a [`Parse`] operator and select [`TimestampedDigitalOutputSet`] from the `Register` property dropdown menu.
- Run the workflow, start the pulse train with the `A` key and open the visualizer for the [`Parse`] operator.  
**What do you observe?**

Turning the events off reduces the number of messages sent by the device, and allows faster pulse trains on some outputs. See [Pulse trains](device.md#pulse-trains) for the limits.

- Insert another [`KeyDown`] operator and set the `Filter` property to the key `D`.
- Insert a [`CreateMessage`] operator, select [`PulseEventEnablePayload`] for the `Payload`, and `None` for the [`PulseEventEnable`] property.
- Insert a [`MulticastSubject`] operator and configure the `Name` property to `Hobgoblin Commands`.
- Run the workflow, start the pulse train with the `A` key and press the `D` key. What happens to the events? Is the LED still blinking?
- Change the `PulseEventEnable` property to `GP18`, run the workflow again, start the pulse train and press the `D` key. Are the events reported now?

>[!NOTE]
> The [`PulseEventEnable`] register acts on all the digital outputs at once, and any output that is not selected stops reporting pulse events.

>[!NOTE]
> **Optional**: Verify the pulse train by connecting the output to a digital input pin and reading the [`TimestampedDigitalInputState`].

>[!TIP]
> If your camera supports external triggering, you can use pulse trains to trigger frame capture. Recording the same pulse train on a digital input with the `Hobgoblin` allows you to have hardware timestamped images that are automatically aligned with other acquired data.

### Exercise 4: Triggering a digital line based on region of interest activity

:::workflow
![Hobgoblin Region of Interest](../workflows/hobgoblin-closeloop-roi.bonsai)
:::

- Connect the LED module back to digital output channel `GP15` on the `Hobgoblin`.
- Insert a [`VideoCaptureDevice`] operator. Set the `Index` property to the right camera.
- Insert a [`Crop`] transform.
- Run the workflow and set the `RegionOfInterest` property to specify the desired area.
- Insert a [`Grayscale`] transform.
- Insert a [`Threshold`] transform. You can use the color segmentation operators instead of these two transforms.
- Insert a [`Sum`] transform, and select the `Val0` field from the output.
- Insert a [`GreaterThan`] transform and configure the `Value` property to an appropriate threshold. Remember you can use the visualizers to see what values are coming through the [`Sum`] and what the result of the [`GreaterThan`] operator is.
- Insert a [`DistinctUntilChanged`] operator, so that the LED is toggled only when the result changes, and not on every frame.
- Insert a [`CreateMessage`] operator, select [`DigitalOutputTogglePayload`] for the `Payload`, and `GP15` for the [`DigitalOutputToggle`] property.
- Insert a [`MulticastSubject`] operator and configure the `Name` property to `Hobgoblin Commands`.
- Run the workflow and verify that the LED toggles when entering or leaving the region of interest.
- **Optional:** Replace the [`Crop`] transform by a [`CropPolygon`] to allow for non-rectangular regions.
- **Optional:** Modify the workflow to replace the digital output toggle with a pulse train.

> [!Note]
> The [`CropPolygon`] operator uses the `Regions` property to define multiple, possibly non-rectangular regions. The visual editor is similar to [`Crop`], where you draw a rectangular box. However, in [`CropPolygon`] you can move the corners of the box by right-clicking _inside_ the box and dragging the cursor to the new position. You can add new points by double-clicking with the left mouse button, and delete points by double-clicking with the right mouse button. You can delete regions by pressing the `Del` key and cycle through selected regions by pressing the `Tab` key.

### Exercise 5: Conditioned place preference

Implement the following trial structure for conditioned place preference. `enter` and `leave` events should be triggered in real-time from the camera, by tracking an object moving in or out of a region of interest (ROI). `Reward` should be triggered once upon entering the ROI, and not repeat again until the object exits the ROI and the ITI has elapsed.

```mermaid
stateDiagram-v2
    direction LR
    ITI --> Ready: elapsed
    Ready --> Reward: enter
    Reward --> ITI: leave
```

> [!Tip]
> There are several ways to implement ROI activation, so feel free to explore different ideas. Consider using either [`Crop`], [`RoiActivity`], or [`ContainsPoint`] as part of different strategies to implement the `enter` and `leave` events.

<!--Reference Style Links -->
[`BitwiseNot`]: xref:Bonsai.Expressions.BitwiseNotBuilder
[`ContainsPoint`]: xref:Bonsai.Vision.ContainsPoint
[`CreateMessage`]: xref:Harp.Hobgoblin.CreateMessage
[`Crop`]: xref:Bonsai.Vision.Crop
[`CropPolygon`]: xref:Bonsai.Vision.CropPolygon
[`Device`]: xref:Harp.Hobgoblin.Device
[`Difference`]: xref:Bonsai.Dsp.Difference
[`DigitalOutputClear`]: xref:Harp.Hobgoblin.DigitalOutputClear
[`DigitalOutputClearPayload`]: xref:Harp.Hobgoblin.CreateDigitalOutputClearPayload
[`DigitalOutputSet`]: xref:Harp.Hobgoblin.DigitalOutputSet
[`DigitalOutputToggle`]: xref:Harp.Hobgoblin.DigitalOutputToggle
[`DigitalOutputTogglePayload`]: xref:Harp.Hobgoblin.CreateDigitalOutputTogglePayload
[`DistinctUntilChanged`]: xref:Bonsai.Reactive.DistinctUntilChanged
[`Grayscale`]: xref:Bonsai.Vision.Grayscale
[`GreaterThan`]: xref:Bonsai.Expressions.GreaterThanBuilder
[`KeyDown`]: xref:Bonsai.Windows.Input.KeyDown
[`MulticastSubject`]: xref:Bonsai.Expressions.MulticastSubject
[`Parse`]: xref:Harp.Hobgoblin.Parse
[`PulseEventEnable`]: xref:Harp.Hobgoblin.PulseEventEnable
[`PulseEventEnablePayload`]: xref:Harp.Hobgoblin.CreatePulseEventEnablePayload
[`PulseTrainGP18`]: xref:Harp.Hobgoblin.PulseTrainGP18
[`PulseTrainGP18Payload`]: xref:Harp.Hobgoblin.CreatePulseTrainGP18Payload
[`RoiActivity`]: xref:Bonsai.Vision.RoiActivity
[`SubscribeSubject`]: xref:Bonsai.Expressions.SubscribeSubject
[`Sum`]: xref:Bonsai.Dsp.Sum
[`Threshold`]: xref:Bonsai.Vision.Threshold
[`TimestampedDigitalInputState`]: xref:Harp.Hobgoblin.TimestampedDigitalInputState
[`TimestampedDigitalOutputSet`]: xref:Harp.Hobgoblin.TimestampedDigitalOutputSet
[`TimestampedDigitalOutputToggle`]: xref:Harp.Hobgoblin.TimestampedDigitalOutputToggle
[`VideoCaptureDevice`]: xref:Bonsai.Video.VideoCaptureDevice
