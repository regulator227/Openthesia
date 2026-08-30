# Openthesia

Openthesia is a piano-learning and MIDI-visualization product for self-directed beginner-to-intermediate keyboard learners and performers. It keeps musical content, personal learning data, and device configuration conceptually separate.

## Language

**Learner**:
A durable local profile for a person using guided practice to improve their accuracy, timing, and fluency. An Openthesia installation may have multiple Learners.
_Avoid_: Student, user, account, device

**Active Learner**:
The Learner whose personal learning data applies to the current use of Openthesia. Selecting the Active Learner is a Device Setting.

**Practice Session**:
A Learner's single comparable attempt to improve a fixed range of a Chart under one Practice Mode and one evaluation-relevant configuration. It ends completed or abandoned; restarting or changing an evaluation-relevant choice begins another Practice Session.
_Avoid_: Lesson, run

**Practice Mode**:
A named policy for how Chart time, guidance, and feedback behave during a Practice Session. Hand choice, accompaniment, tempo, range, guidance preset, and visual view configure a Practice Session but are not Practice Modes.
_Avoid_: Game mode, playback mode

**Wait for Notes**:
A Practice Mode in which Chart time waits until the Learner supplies the required notes.
_Avoid_: Learning Mode, Melody Practice, Play Along

**Play in Time**:
A Practice Mode in which Chart time continues while the Learner receives visible guidance and performance feedback.
_Avoid_: Rhythm Practice

**Recital**:
A Practice Mode in which Chart time continues with minimal guidance and performance feedback is deferred until the attempt ends.

**Practice Target**:
The set of unique pitches from the Required Hands that share one normalized Chart onset in Wait for Notes. It is satisfied by distinct note attacks made after it becomes due.

**Practice Event**:
An ordered fact emitted during a Practice Session about lifecycle, Learner input, Practice Targets, or assistance. It records what happened without assigning a score or updating Practice Progress itself.
_Avoid_: Score event, analytics event

**Practice Result**:
The completed assessment of one ended Practice Session, keeping Completion, Accuracy, and Timing separate and identifying whether the attempt is eligible for Practice Progress.
_Avoid_: Score, grade, rating

**Comparable Practice Setup**:
The Chart, Practice Mode, Required Hands, range, tempo, Accompaniment, and Scoring Policy under which eligible Practice Results may establish personal bests or recent trends.

**Timing Judgment**:
A learner-facing classification of a required note opportunity or Learner attack as Fantastic, Early, Late, Miss, or Extra.

**Scoring Policy**:
The versioned rules that translate Practice Events into Practice Results so outcomes created under different rules are never silently compared.

**Personal Best**:
The strongest eligible Accuracy or Timing outcome achieved by a Learner within one Comparable Practice Setup.
_Avoid_: High score, leaderboard position

**Song**:
A musical work available for learning. A Song has a stable identity and one or more Charts.
_Avoid_: MIDI file, track

**Chart**:
A distinct playable note-and-timing pattern for a Song. Its identity follows normalized pitch, onset, duration, and tempo rather than file location, descriptive metadata, or MIDI encoding details; changing that pattern produces a new Chart, and a Chart belongs to exactly one Song.
_Avoid_: MIDI file, track, song version

**MIDI Source**:
A device-owned reference to a user-owned MIDI file from which Openthesia reads a Chart. Multiple MIDI Sources may present the same Chart; their filenames and locations do not define the Chart's identity.
_Avoid_: Song, Chart

**MIDI Search Path**:
A device-owned folder configured as a root under which Openthesia discovers MIDI Sources. Its folder hierarchy organizes source selection but does not define Song or Chart identity.
_Avoid_: Song Library, playlist

**MusicXML Source**:
A device-owned reference to a user-owned MusicXML file from which Openthesia reads a Chart and an authored Score Interpretation. Its filename and location do not define Song or Chart identity.
_Avoid_: Song, Chart, Score Interpretation

**Hand Assignment**:
A Chart-owned classification of its notes as left-hand or right-hand parts, shared by all Learners.
_Avoid_: Staff placement, fingering

**Required Hands**:
The left hand, right hand, or both Hand Assignment parts that the Learner must perform during a Practice Session.
_Avoid_: Active hands, enabled hands

**Accompaniment**:
Automatic playback of Chart notes outside the Required Hands during a Practice Session. It may be automatic or silent and is not a Practice Mode.
_Avoid_: Backing track, disabled hand

**Song Metadata**:
Shared descriptive facts, including descriptive tags, that apply to a Song across all of its Charts.

**Chart Metadata**:
Shared arrangement-specific facts and learning annotations, including arrangement difficulty, that apply to a Chart for every Learner.

**Learner Song Data**:
The personal organization and preferences belonging to one Learner for one Song, including group membership and favorite state.
_Avoid_: Song Metadata, Learner Chart Data

**Learner Chart Data**:
The personal choices and learning outcomes belonging to one Learner for one Chart.
_Avoid_: Song Metadata, Device Settings

**Device Settings**:
Hardware and installation-specific configuration that does not belong to a Song, Chart, or Learner.

**Lighted Keyboard Guidance**:
An optional Device Setting that presents the next Required Hands Practice Target on compatible illuminated keys during Wait for Notes or Play in Time. It is unavailable in Recital and does not define vendor-specific lighting behavior.
_Avoid_: Keyboard lesson mode, light show

**Song Library**:
A Learner's locally managed view of Songs, shared metadata, personal organization, and Chart-specific Practice Progress.
_Avoid_: Folder browser, song store

**Practice Progress**:
The durable record of how one Learner's fluency with one Chart changes across eligible Practice Results. Song-level summaries are derived from Chart-specific Practice Progress.
_Avoid_: High score, account statistics

**Performance Visualization**:
A live or recorded visual representation of a performance that does not require the performer to follow a guided practice sequence.
_Avoid_: Practice mode, lesson mode

**Accessibility Baseline**:
The always-on guarantees that keep the learner-facing Practice journey operable and meaningful across scaling, keyboard input, focus, color perception, motion sensitivity, and alternative labels.
_Avoid_: Accessibility mode, compliance certification

**Practice Score**:
A read-only staff-notation view of a selected Score Interpretation, synchronized with a Practice Session to support learning rather than notation authoring or publication.
_Avoid_: Sheet-music editor, notation workspace, score editor

**Score Interpretation**:
A Chart-owned notation representation derived from its playable pattern or imported from MusicXML. A Chart may have multiple Score Interpretations without changing identity.
_Avoid_: Chart, MusicXML file, score version

**Capability Adaptation**:
An independently designed Openthesia capability inspired by behavior proven in another product, selected for fit with Openthesia rather than parity.
_Avoid_: Code port, clone feature
