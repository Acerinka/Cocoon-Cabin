# Interaction model and evidence

## One concept, two implementations

The hand-gesture application studies recognition and confirmation through a camera. The Unity/Quest application studies the complete passenger journey in space. They share an interaction concept, not a live data connection.

| | Web gesture prototype | Unity / Quest prototype |
| --- | --- | --- |
| Input | Camera hand landmarks and geometric shape rules. | Tracked XR poses and controller inputs. |
| Initial action | Open palm held for 2 seconds. | Raised-hand pose held for 2 seconds, with spatial checks. |
| Confirmation | Thumbs-up held for 2 seconds within an 8-second window. | A further 3-second pose hold. |
| Focus | Recognition progress, temporal confirmation and recovery. | Coordinated entry, seating, luggage, screens, travel and exit. |
| Connection | Standalone web application. | Standalone Unity application; no live web event bridge. |

Web evidence was reviewed at [`c364de64c41b0ec6303a1315a6fe2cc5e0a68057`](https://github.com/MoeMaelGit/AHMI_HandGesture_Demo_00/tree/c364de64c41b0ec6303a1315a6fe2cc5e0a68057).

## Gesture state and recovery

```text
IDLE → DETECTING → PROMPTING → CONFIRMING → CONFIRMED
        palm hold   window      thumb hold   request locked
        2 seconds   ≤8 seconds  2 seconds
```

Tracking is per hand. Brief loss can retain a track; timeout or sustained loss resets the attempt. The first track to finish confirmation locks the request. Multiple hand tracks are not proof of distinct identities or a validated count of concurrent users.

## Boarding sequence

1. Stop the taxi before card-based entry.
2. Recognise the right-hand virtual card at the door-side reader.
3. Deploy the ramp and open the door when entry conditions are met.
4. Let the passenger and luggage move into the aisle.
5. Move the whole seat to its clearance position.
6. Store luggage along the authored path and show storage-area feedback.
7. Enable seating; move the viewpoint to the seat anchor and switch cabin screens.

## Ride and exit sequence

The shared screen communicates route, stops and trip status. Personal armrest screens show the interface concept near the seat. In this prototype, these are stage-based image sequences rather than fully interactive music, climate or navigation services.

At the destination, the taxi stops, displays arrival, makes space and returns luggage. The door and ramp then open for exit. After a preset waiting interval, the cabin resets and the taxi departs. The wait is a simulation condition, not sensed proof that every passenger has left.

## Research to design

| Evidence in the course work | Design response |
| --- | --- |
| Uncertainty at finding, identifying and entering the car. | Separate detection from confirmation and make progress visible. |
| A desire for control, quiet and privacy. | Clear spatial and screen feedback without requiring a conversational companion. |
| Luggage and body positions complicate entry. | Sequence the ramp, door, seat clearance and luggage storage. |
| Reliance on a phone can interrupt access. | Explore on-street gestures and card entry; other recovery routes remain conceptual. |

The English portfolio translates and edits the approved nine-page Chinese case study. Journey charts preserve the relative shape of the team's qualitative maps. No success rates, trust improvements or participant totals have been invented from those charts or from the 3 / 2 / 1 voting weights.
