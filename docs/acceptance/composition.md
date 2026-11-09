# Composition

Composing features for a target (`src/Aioros.Runtime/Composition`).

2 criteria. Each has an identifier and exactly one test.

### COMPOSITION-01: A feature registers its services for a target it supports

- **Given** a feature that supports the target
- **When** the composition is built
- **Then** it succeeds with the feature's service registrations.

### COMPOSITION-02: A feature is rejected on a target it does not support

- **Given** a feature that does not support the target
- **When** the composition is built
- **Then** it fails with "<feature> does not support <target>".
