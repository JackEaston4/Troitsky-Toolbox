# Troitsky Toolbox — Member Needs & Service Priority Survey

Methodology record for the first survye conducted, collecting general member needs and service priority. 
This will be used in addition to complete interviews, to comprise the Fall CD (Interviews)

Methodology record for the instrument as actually administered. This file is the source
for SRS section **P.7 (Requirements process and report)** and for the CD-Fall
User/Stakeholder/Client interview report.

Instrument: Google Forms, titled *Troitsky Toolbox — Member Needs & Service Priority Survey*. 11 questions, anonymous.

---

## 1. Survey Info

- **Population:** McMaster Troitsky Club members — team/general members, team captains,
  coaches, and executive members. Alumni are not excluded.
- **Recruitment:** voluntary, through the club's own communication channels.
- **Sample type:** self-selecting convenience sample. Not random.
- **Anonymity:** no name, student number, MacID, or email is collected. Email collection
  is disabled in the form. Response limiting, which would require sign-in and therefore
  identify respondents, is disabled.

Some questions used to generalize studetn club experience used, while not directly identifiable to the student.

## 2. The Survey

### Introduction text (as presented)

> During the fall and winter terms, a software capstone group will be creating a
> website/service to support the McMaster Troitsky club. The development process is
> aiming to be heavily intertwined with the club for this coming year to create a service
> that is highly tailored to the needs and current limitations of the club.
>
> This anonymous survey serves to collect interest and priority of concerns within the
> club. Participation is voluntary, any question may be skipped.

### Questions and traceability

Every question is tagged with the SRS section its data feeds.

| # | Question | Type | Feeds |
|---|---|---|---|
| 1 | How many years of experience do you have in Troitsky? (0–5) | Single select |
| 2 | What is your current role(s)? (Team/General Member, Team Captain, Coach, Executive Member) | Multi select |
| 3 | What is your modelling experience? (None / Through some courses / Experience modelling in and out of class) | Single select |
| 4 | How valuable would you find an entry level modelling service, implementing some basic features and model feature analysis? (1–5) | Linear scale |
| 5 | Value of easy access to past McMaster Troitsky data, per type: bridge modelling file; joint construction data; manufacturing information; past team results; past technical presentation material (1–5 each) | Grid |
| 6 | How does your team store data? (Microsoft Teams, Google Drive, individual people hold files, Discord, email, other) | Single select |
| 7 | What has been one of the most frustrating parts of your time in the club, outside of actually building the bridge? | Paragraph |
| 8 | Rate features A–K on how useful you would find them (Would not use → Club needs this) | Grid |
| 9 | If we could deliver only 3 of these, which would you choose? (A–K, up to 3) | Multi select |
| 10 | Would you want your team files, results and progress to be available for future teams to learn from? (Yes / No / Maybe) | Single select |
| 11 | If you could go back to being a new member, or if you're currently a new member, what tutorials would you want access to? (Modelling software, manufacturing, slab building, team organization, other — select 2) | Multi select |

**Feature list used in Q8 and Q9:**

A. Searchable archive of past bridge designs
B. Database of material testing results (stick strength, glue, floss, etc.)
C. Bridge weight estimator from a submitted model
D. Structural efficiency calculation
E. Ultimate load and failure point prediction
F. Upload and visualise a team's bridge model in the browser
G. Exit documents / role handover records for executive positions
H. Onboarding guide and training material for new members
I. Private workspace for each team's own files and data
J. Competition rules and regulations reference
K. Club announcements and general information page

## 3. Data Collection

1. Export responses to CSV.
2. Review every free-text answer for identifying information and redact before
   committing, ensure written answers are usable.
3. Commit the confirmed CSV to `docs/CDs/Interviews/` as the raw data appendix.
