# Module Guide (MG)

The Module Guide specifies the modular structure of the Troitsky Toolbox. The
system is decomposed by information hiding, so each module hides a "secret"
that represents a likely future change, such as yearly competition rule
updates or new tools. It is written for the designers of the system and for
future members of the club's software subteam who take over maintenance.

**Rendered document:** [MG.pdf](https://jackeaston4.github.io/Troitsky-Toolbox/Design/SoftArchitecture/MG.pdf)

## Contents

[MG.tex](MG.tex) is the whole document:

| Section | What it covers |
|---|---|
| Reference Material | Abbreviations and acronyms. |
| Introduction | The decomposition approach and the intended readers of the document. |
| Anticipated and Unlikely Changes | The changes the design is built to accommodate, and those it assumes will not happen. |
| Module Hierarchy | The modules (M1 to M20), grouped into Hardware-Hiding, Behaviour-Hiding and Software Decision modules. |
| Connection Between Requirements and Design | How the requirements in the SRS shaped the design. |
| Module Decomposition | The secret, services and implementation of each module. |
| Traceability Matrix | Traces from requirements to modules and from anticipated changes to modules. |
| Use Hierarchy Between Modules | The uses relation between modules. |
| User Interfaces, Communication Protocols and Database Design | The user interface, communication between components, and the database entities and the module that owns each. |
| Timeline | The implementation schedule. |
| Generative AI Use Disclosure | The AI tools used, how and where they were used, and how their output was verified. |

The interface of each module is specified in the
[Module Interface Specification](../SoftDetailedDes/). References come from the
shared [refs/References.bib](../../../refs/References.bib).
