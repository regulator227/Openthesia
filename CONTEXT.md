# Openthesia

Openthesia is a piano-learning and MIDI-visualization product for self-directed beginner-to-intermediate keyboard learners and performers. It keeps musical content, personal learning data, and device configuration conceptually separate.

## Language

**Learner**:
A durable local profile for a person using guided practice to improve their accuracy, timing, and fluency. An Openthesia installation may have multiple Learners.
_Avoid_: Student, user, account, device

**Active Learner**:
The Learner whose personal learning data applies to the current use of Openthesia. Selecting the Active Learner is a Device Setting.

**Practice Session**:
A learner's focused attempt to improve part or all of a song, including any guidance and feedback received during that attempt.
_Avoid_: Lesson, run

**Practice Mode**:
A defined pattern of guidance and feedback applied during a Practice Session, such as waiting for correct notes or evaluating performance in time.
_Avoid_: Game mode, playback mode

**Song**:
A musical work available for learning. A Song has a stable identity and one or more Charts.
_Avoid_: MIDI file, track

**Chart**:
A distinct playable note-and-timing pattern for a Song. Its identity follows normalized pitch, onset, duration, and tempo rather than file location, descriptive metadata, or MIDI encoding details; changing that pattern produces a new Chart, and a Chart belongs to exactly one Song.
_Avoid_: MIDI file, track, song version

**MIDI Source**:
A device-owned reference to a user-owned MIDI file from which Openthesia reads a Chart. Multiple MIDI Sources may present the same Chart; their filenames and locations do not define the Chart's identity.
_Avoid_: Song, Chart

**Hand Assignment**:
A Chart-owned classification of its notes as left-hand or right-hand parts, shared by all Learners.
_Avoid_: Staff placement, fingering

**Song Metadata**:
Shared descriptive facts that apply to a Song across all of its Charts.

**Chart Metadata**:
Shared arrangement-specific facts and learning annotations that apply to a Chart for every Learner.

**Learner Chart Data**:
The personal choices and learning outcomes belonging to one Learner for one Chart.
_Avoid_: Song Metadata, Device Settings

**Device Settings**:
Hardware and installation-specific configuration that does not belong to a Song, Chart, or Learner.

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
