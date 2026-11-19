# CD-Fall: Stakeholder Interview Report

This folder is the team's **Fall Custom Document** submission
(User/Stakeholder/Client interview report).

**Rendered report:** [InterviewReport.pdf](https://jackeaston4.github.io/Troitsky-Toolbox/CDs/Interviews/InterviewReport.pdf)

## Contents

| File / folder | What it is |
|---|---|
| [InterviewReport.tex](InterviewReport.tex) | The full interview report, published to pdf. |
| [InterviewGuide.md](InterviewGuide.md) | The interview guide, considerations, format and the question sets used in the sessions. |
| [InterviewRecordTemplate.xlsx](InterviewRecordTemplate.xlsx) | Blank record sheet filled in by the notetaker for each session. |
| [InterviewRecords/](InterviewRecords/) | Anonymized raw records, one CSV per session (`InterviewRecord_GroupN.csv`), and the `.text` file of each that the report includes as Appendix A. |
| [RequirementElicitationSurvey/](RequirementElicitationSurvey/) | The earlier member survey: its design record and raw responses. The report compares the interview results against it. |

## Building

From the repository root: `make docs/CDs/Interviews/InterviewReport.pdf`.
The appendix fragments in `InterviewRecords/` use the `.text` extension so CI
does not try to build them as standalone documents.
