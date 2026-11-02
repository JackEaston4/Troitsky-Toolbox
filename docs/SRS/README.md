# Software Requirements Specification (SRS)

The SRS for the Troitsky Toolbox follows the four-book structure from Bertrand
Meyer's *Handbook of Requirements and Business Analysis*, with an Appendix book
added for content the course requires that fits none of the four (see A.3).

[SRS.tex](SRS.tex) is the main document. It holds the title page and revision
history, then `\input`s one file per section from the folders below. CI builds
it into [pdfs/SRS/SRS.pdf](../../pdfs/SRS/SRS.pdf).

## Books

| Book | Folder | What it covers |
|---|---|---|
| **G** - Goals | [Goals/](Goals/) | Why the project exists: the context, the current situation, expected benefits, a functionality overview, high-level usage scenarios, limitations and stakeholders. Written for the club and other non-technical readers. |
| **E** - Environment | [Environment/](Environment/) | The world the system operates in and does not control: the glossary, external components, constraints, assumptions, effects and invariants. |
| **S** - System | [System/](System/) | The system itself: its components, the functional and non-functional requirements, interfaces, detailed usage scenarios, prioritization, verification criteria and formalization. |
| **P** - Project | [Project/](Project/) | How the system is built: roles, imposed technical choices, schedule, tasks and deliverables, required technology, risks, and the requirements process. |
| **A** - Appendix | [Appendix/](Appendix/) | Symbolic constants, the traceability matrix, changes from the Meyer template, the generative AI disclosure and the team reflection. |

Each section is a single file named after its number (for example `S2.txt` is
S.2 Functionality). The E.1 Glossary is placed before the Goals book in the PDF
so terms are defined before their first use. References come from the shared
[refs/References.bib](../../refs/References.bib) and are printed between the
Project and Appendix books.

## Adding or changing a section

- Edit the section's `.txt` file; it starts with `\nsection{X.n Title}`.
- A new section needs a new file and a matching `\input` line in
  [SRS.tex](SRS.tex).
- Any departure from Meyer's structure should be recorded in A.3.
