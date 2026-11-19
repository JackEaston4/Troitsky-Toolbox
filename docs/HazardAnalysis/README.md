# Hazard Analysis

This document identifies the ways the Troitsky Toolbox could fail or be
misused, assesses their effects on the club and its members, and derives the
safety and security requirements (prefixed SR) that prevent or mitigate them.
The Toolbox controls no physical equipment, so the hazards are mainly to the
club's data and decisions, along with the indirect risk of a team trusting an
incorrect failure-load estimate.

**Rendered document:** [HazardAnalysis.pdf](https://jackeaston4.github.io/Troitsky-Toolbox/HazardAnalysis/HazardAnalysis.pdf)

## Contents

[HazardAnalysis.tex](HazardAnalysis.tex) is the whole document:

| Section | What it covers |
|---|---|
| Introduction | Background on the project and the definition of a hazard used throughout the document. |
| Scope and Purpose | What the analysis sets out to do and how its results feed the design and verification. |
| System Boundaries and Components | The components inside the system boundary that the team controls, and the external entities outside it. |
| Critical Assumptions | Assumptions the analysis relies on. |
| Failure Mode and Effect Analysis | The FMEA table: failure modes, effects, causes, severity and occurrence ratings, recommended actions, and the SR and SRS requirements that address each. |
| Safety and Security Requirements | The new functional and non-functional SR requirements derived from the FMEA. |
| Roadmap | Which SR requirements are planned during the capstone and which are delayed. |
| Appendix | Changes from the course template, the generative AI disclosure and the team reflection. |

References come from the shared
[refs/References.bib](../../refs/References.bib).
