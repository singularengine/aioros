# Events

The event bus (`src/Aioros.Runtime/Events`).

2 criteria. Each has an identifier and exactly one test.

### EVENTS-01: A subscriber receives the events of its type in order

- **Given** a subscriber to one event type
- **When** events of several types are published
- **Then** it receives only its type, in publishing order.

### EVENTS-02: Every subscriber receives every event

- **Given** two subscribers to the same type
- **When** one event is published
- **Then** each of them receives it.
