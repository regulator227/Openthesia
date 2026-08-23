# Openthesia

Openthesia is a piano-learning and MIDI-visualization product for self-directed beginner-to-intermediate keyboard learners and performers.

## Language

**Learner**:
A person using guided practice to learn a song or improve their accuracy, timing, and fluency.
_Avoid_: Student, player account

**Practice Session**:
A learner's focused attempt to improve part or all of a song, including any guidance and feedback received during that attempt.
_Avoid_: Lesson, run

**Practice Mode**:
A defined pattern of guidance and feedback applied during a Practice Session, such as waiting for correct notes or evaluating performance in time.
_Avoid_: Game mode, playback mode

**Song**:
A playable musical work together with the learning metadata Openthesia uses to present and practice it.
_Avoid_: MIDI file, track

**Chart**:
A specific playable arrangement of a Song, identified by its normalized pitches, timing, durations, and tempo. A Song can have more than one Chart.
_Avoid_: MIDI file, track, song version

**Hand Assignment**:
A Chart-owned classification of its notes as left-hand or right-hand parts, shared by all Learners.
_Avoid_: Staff placement, fingering

**Song Library**:
A learner's locally managed collection of Songs, including their descriptive metadata, organization, and Practice Progress.
_Avoid_: Folder browser, song store

**Practice Progress**:
The durable record of how a Learner's fluency with a Song changes across Practice Sessions.
_Avoid_: High score, account statistics

**Performance Visualization**:
A live or recorded visual representation of a performance that does not require the performer to follow a guided practice sequence.
_Avoid_: Practice mode, lesson mode

**Practice Score**:
A read-only staff-notation view of a selected Score Interpretation, synchronized with a Practice Session to support learning rather than notation authoring or publication.
_Avoid_: Sheet-music editor, notation workspace, score editor

**Score Interpretation**:
A Chart-owned notation representation derived from its playable pattern or imported from MusicXML. A Chart may have multiple Score Interpretations without changing identity.
_Avoid_: Chart, MusicXML file, score version

**Capability Adaptation**:
An independently designed Openthesia capability inspired by behavior proven in another product, selected for fit with Openthesia rather than parity.
_Avoid_: Code port, clone feature
