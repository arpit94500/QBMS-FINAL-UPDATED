# Delivery 2: activity log + pagination

- **Activity log module**: table `user_activity_log` (script in `Database/user_activity_log.sql`; it is also created automatically on first use). A global action filter (`ActivityLogActionFilter`) records every action of a logged-in user (personal no., name, role, controller/action, method, path, query string without secret-looking keys, IP, OK/FAILED). Login success and failed attempts are logged by `AccountController` (the password is never logged). Logging errors never break the application.
  Admin page: **Admin > Activity Logs** (`/Admin/ActivityLogs`) with filters (personal no., role, date range, search) and server-side paging.
- **Pagination for all tables**: `qbms-ui.js` pages every table (10/25/50/100 rows per page, first/previous/next/last), works with the existing live filters, and CSV export still exports all pages. Add `data-no-paginate` to a table to opt out (the activity log table does, as it pages on the server).
- Not compiled here; please build and test.

---

# QBMS changes (latest delivery)

Not compiled or run here (no .NET SDK / database available): please build, then test the items below.

## Account
- **Login password is no longer sent as plain text.** The login page receives an RSA public key and a one-time challenge; the browser encrypts the password (RSA-OAEP / SHA-256, WebCrypto) and posts only `EncryptedPassword`. The visible password box has no `name`, so the plain value is never in the request. The server decrypts it (`QBMS1/Services/LoginPasswordCrypto.cs`), rejects a challenge that was already used (no replay) and then validates as before.
  Files: `Login.cshtml`, `wwwroot/js/login-crypto.js`, `LoginViewModel`, `AccountController`, `Startup` (adds `AddMemoryCache` + singleton).
  Notes: WebCrypto needs HTTPS (or localhost). The key pair is created at start-up, so a login page opened before an application restart must be reloaded. Change Password still posts plain text (not part of this request).

## Setter
- **Dashboard**: Level, Topic and Factor columns removed (both tables). New **Progress** column (green = created, red = still to create). Per exam row, in front of *Create Question*: **Download Excel / Download Word / Upload Excel / Upload Word**.
- **Duplicate Check Status** block now sits after the exam / assignment table.
- **/Setter/UploadQuestions** is split into **1. Download template** and **2. Upload completed template**. The dashboard Upload buttons open it with that exam preselected and only the matching (Excel or Word) upload form.
- **Template file names** follow the TOS-assignment hierarchy, e.g. `Exam_Paper_Section_SubSection_Topic_Template.xlsx` (only the levels the assignment covers).
- **QB_Maker** in the Excel and Word templates is the Setter's **personal number** (`personal_number`); upload validation checks the same value (old name-based templates are still accepted).
- **Create Question**: quota is shown at the top (assignment quota + selected topic quota); prefilled values (Exam, Paper, Section, SubSection, Topic, Question Type, Difficulty) are bold text, not boxes; topic and difficulty are pre-selected from the TOS quota (first topic in TOS order that still needs questions, EASY then MEDIUM then HARD, multiplication factor applied); option boxes are smaller, two per line.

## Admin
- **TOS Assignment**: the *Complete Exam* level is removed (also rejected on the server; existing EXAM assignments still work). Only the hierarchy fields that apply to the chosen level are shown (Paper: Exam+Paper, Section: +Section, SubSection: +SubSection, Topic: +Topic).
- **Setter / Vetter drop-downs** show `personal number - name`.
- **Progress bars**: red = not completed, green = completed (assignment management, reports, setter dashboard). In the stacked report bars the unfinished part is red and *Rejected* is now amber so it is not confused with it.
- **Reports**: *Exam-wise progress* replaced by **Paper-wise progress** (new `ReportRepository.GetPaperProgressAsync`). An assignment counts once for every paper it covers; targets are rounded up per paper, so the paper rows can differ by a question or two from the assignment totals.

---

# QBMS changes (latest delivery)

Not compiled or run here (no .NET SDK / database available): please build, then test the items below.

## Account
- **Login password is no longer sent as plain text.** The login page receives an RSA public key and a one-time challenge; the browser encrypts the password (RSA-OAEP / SHA-256, WebCrypto) and posts only `EncryptedPassword`. The visible password box has no `name`, so the plain value is never in the request. The server decrypts it (`QBMS1/Services/LoginPasswordCrypto.cs`), rejects a challenge that was already used (no replay) and then validates as before.
  Files: `Login.cshtml`, `wwwroot/js/login-crypto.js`, `LoginViewModel`, `AccountController`, `Startup` (adds `AddMemoryCache` + singleton).
  Notes: WebCrypto needs HTTPS (or localhost). The key pair is created at start-up, so a login page opened before an application restart must be reloaded. Change Password still posts plain text (not part of this request).

## Setter
- **Dashboard**: Level, Topic and Factor columns removed (both tables). New **Progress** column (green = created, red = still to create). Per exam row, in front of *Create Question*: **Download Excel / Download Word / Upload Excel / Upload Word**.
- **Duplicate Check Status** block now sits after the exam / assignment table.
- **/Setter/UploadQuestions** is split into **1. Download template** and **2. Upload completed template**. The dashboard Upload buttons open it with that exam preselected and only the matching (Excel or Word) upload form.
- **Template file names** follow the TOS-assignment hierarchy, e.g. `Exam_Paper_Section_SubSection_Topic_Template.xlsx` (only the levels the assignment covers).
- **QB_Maker** in the Excel and Word templates is the Setter's **personal number** (`personal_number`); upload validation checks the same value (old name-based templates are still accepted).
- **Create Question**: quota is shown at the top (assignment quota + selected topic quota); prefilled values (Exam, Paper, Section, SubSection, Topic, Question Type, Difficulty) are bold text, not boxes; topic and difficulty are pre-selected from the TOS quota (first topic in TOS order that still needs questions, EASY then MEDIUM then HARD, multiplication factor applied); option boxes are smaller, two per line.

## Admin
- **TOS Assignment**: the *Complete Exam* level is removed (also rejected on the server; existing EXAM assignments still work). Only the hierarchy fields that apply to the chosen level are shown (Paper: Exam+Paper, Section: +Section, SubSection: +SubSection, Topic: +Topic).
- **Setter / Vetter drop-downs** show `personal number - name`.
- **Progress bars**: red = not completed, green = completed (assignment management, reports, setter dashboard). In the stacked report bars the unfinished part is red and *Rejected* is now amber so it is not confused with it.
- **Reports**: *Exam-wise progress* replaced by **Paper-wise progress** (new `ReportRepository.GetPaperProgressAsync`). An assignment counts once for every paper it covers; targets are rounded up per paper, so the paper rows can differ by a question or two from the assignment totals.

---

# Previous delivery

Not compiled or run here (no .NET SDK / database / Python API available): please build, then test the items below.

## Admin
- **UI**: one new stylesheet `wwwroot/css/qbms-ui.css` and one script `wwwroot/js/qbms-ui.js`, loaded from `_Layout`. Forms now have proper spacing, glass inputs, tables, progress bars. No existing id/name changed.
- **TOS Management** (assignment form): sectioned glass cards (type / scope / users), link to Assignment Management.
- **TOS Assignment Management**: filters for Exam (by name + ID) and Paper (cascading, by name + ID), server-side search (also section / sub-section), live quick filter, CSV export, progress bar.
- **Target / Completed / Remaining fixed** (`QuestionAssignmentRepository.GetAllForManagementAsync`):
  Target = TOS requirement (ceil(sum of active TOS counts x factor) per difficulty). Completed = questions created for the assignment, capped per difficulty. Remaining = Target - Completed.
  Old query joined every question to every TOS row (inflated counts, one row per TOS name) and counted only final-approved questions.
- **Reports** (`/Admin/Reports`): KPI cards, Setter progress, Vetter progress, Exam-wise progress, exam filter, CSV export.

## Setter
- **Create Question**: page loaded `question-editor.js` and MathJax twice, configured MathJax after loading it, bound Math buttons by a class that did not exist, and ran a stray script that threw. Replaced by one clean `question-editor.js` + shared `qbms-math.js` + `_MathSupport.cshtml` (popup, live preview, display mode, examples).
- **Next / Save not working**: the page offered "MCQ" while saving required "OBJ", and the select was `required` but never preselected for stored "OBJ". Types are now OBJ / SUBJECTIVE (shown as MCQ / Subjective), normalised on save, defaulted from the TOS topic type; options only required for MCQ.
- **Quota**: quota rounding now uses Ceiling (it used Floor, unlike Admin and the templates); difficulty levels with no quota left are disabled in the form; server still enforces.
- **Duplicate check on create**: "Check duplicates" button + automatic check, and server-side block at/above the configured threshold (Python API). If the API is down the question is saved with a warning.
- **Excel / Word upload**: one shared validator (`ValidateUploadRowsAsync`): TOS hierarchy, difficulty, type, marks, QB_Maker, question text, MCQ options + answer, in-file duplicates (HTML/spacing/case ignored), per-topic quota, total quota. Both formats accept partial files and ignore columns after the 16 template columns.
- **Downloaded duplicate-report Word could not be uploaded**: it has 4 extra columns and the Word upload required exactly 16. Fixed (Excel too).

## Vetter
- Review page now includes the math popup and renders saved `\( ... \)` as equations; editor script rewritten; saves keep the LaTeX source.
- Read-only views (Admin Final Approval, Setter Dashboard) now render equations too.
