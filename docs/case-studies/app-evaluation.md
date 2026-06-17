# App Evaluation for Real-World E2E Case Study

To establish AgentLoop's credibility as a robust, non-intrusive UI testing agent, we evaluate 4 popular open-source Windows desktop applications. 

The goal is to select an uncontrolled application that can demonstrate:
1. **Record & Replay**: Recording a user flow and replaying it deterministically.
2. **Selector Drift & Healer**: Moving or renaming controls to test self-healing.
3. **Secret Masking**: Handling password fields securely without leaking credentials in logs/screenshots.
4. **No Intrusion**: Testing the app strictly from the outside without modifying its binary or code.

---

## Candidates Comparison

| Application | Framework / Tech Stack | UI Type | Available Flows | Secret/Masking Proof | UIA Tree Quality | Binary Availability |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **KeePass 2.55+** | .NET Framework 4.8 / WinForms | Standard Native | Login (Master Password), Create Database, Add Entry, Search | **Excellent** (Secure edit fields for Master Password and entry values) | **Excellent** (Standard Win32/WinForms controls fully exposed) | Yes (Portable ZIP / No install required) |
| **DevToys 1.0** | .NET 8 / WPF (Fluent UI) | Modern Responsive | Tool Selection, Text Format/Encode, Settings | **None** (Only standard inputs, no native password forms) | **Good** (Some custom panels, but core textboxes are accessible) | Yes (Portable ZIP available) |
| **ScreenToGif** | .NET 8 / WPF | Custom Heavy | Record Screen, Edit Steps, Save/Export GIF | **Low** (Settings screens only, no database credentials) | **Moderate** (Custom drawing canvas & timeline are complex to automate) | Yes (Portable ZIP / Single executable) |
| **NAPS2** | .NET 6+ / WinForms | Standard Grid | Create Scan Profile, Scan Page, Save PDF | **Low** (No sensitive credential forms) | **Good** (Standard dialogs and profile forms) | Yes (Portable ZIP / MSI) |

---

## Detailed Analysis

### 1. KeePass 2.x (Recommended)
- **Strengths**: 
  - Standard WinForms controls have very stable, predictable UIA properties (IDs, Names, ControlTypes).
  - The Master Password login dialog is the perfect analogue to enterprise Line-of-Business (LOB) apps.
  - Native secure edit fields let us prove the `AGENTLOOP_SECRET_*` environment variable injection and screenshot masking.
  - Zero dependencies: runs out of a folder with no background services or internet connection.
- **Weaknesses**: Legacy aesthetics, but represents the majority of target enterprise LOB applications.

### 2. DevToys
- **Strengths**: Represents modern WPF with Fluent styling. Great for testing text transformation workflows (Format JSON, Base64 encode).
- **Weaknesses**: Lacks database state, credential flows, or secure textboxes, making it less suitable for proving secret-masking features.

### 3. ScreenToGif
- **Strengths**: A highly interactive real-world app.
- **Weaknesses**: Highly visual and timing-dependent. Testing a recording session requires screen capture hooks that can conflict with AgentLoop's own capture logic.

### 4. NAPS2
- **Strengths**: Clean, standard WinForms configuration dialogs.
- **Weaknesses**: Proving scanning functionality requires setting up virtual WIA/TWAIN scanner drivers in the testing environment, creating setup overhead.

---

## Recommendation

We recommend **KeePass 2.x** as the primary E2E case study application for .NET Framework 4.8 / WinForms. It offers a complete login + CRUD + secure field flow that runs standalone without installation or environment prep. 

**Proposed Workflow for the KeePass Case Study:**
1. **Setup**: Download portable KeePass ZIP and launch `KeePass.exe`.
2. **Create DB**: Automate the "New Database" flow (enters master password twice, saves `.kdbx` file).
3. **Login**: Close KeePass, reopen it, and authenticate using `AGENTLOOP_SECRET_MASTER_PW`.
4. **CRUD**: Add a mock credentials entry, save it, and verify it appears in the grid.
5. **Drift & Heal Verification**: Induce selector drift (e.g. by selecting a different entry view mode or altering database options) to demonstrate healing.
