# Comprehensive Patch Functionality

This document describes the comprehensive patch functionality in the Genesys Cloud Extension Audit application.

## Overview

The application now includes a **Comprehensive Patch Plan** feature that allows you to selectively fix different types of extension assignment issues identified during the audit.

## Issue Types

The audit identifies four main categories of issues:

### 1. Missing Extensions
- **Problem**: User has a profile extension, but no extension record exists in the telephony inventory
- **Default Action**: Assign an available extension if one exists, otherwise clear the profile extension
- **Example**: User has extension "1234" in their profile, but extension "1234" doesn't exist in the system

### 2. Duplicate Users
- **Problem**: Multiple users are assigned to the same extension number
- **Default Action**: Keep the first user (alphabetically), reassign others to available extensions or clear them
- **Example**: Both Alice and Bob have extension "5678" - Bob will be reassigned or cleared

### 3. Discrepancies
- **Problem**: Extension record exists but owned by wrong user or wrong owner type
- **Default Action**: Reassert existing (attempt to sync the user profile with the extension record)
- **Example**: User has extension "9012" in profile, but extension "9012" is owned by a queue

### 4. Reassert Consistent (Optional)
- **Problem**: Not really a problem - these are users that look correct
- **Default Action**: Reassert existing (for verification/sync purposes)
- **Example**: User has extension "3456" and it's correctly owned by them in the telephony system
- **Note**: This is optional and disabled by default to avoid unnecessary API calls

## How to Use

### Step 1: Build Context and Generate Plan

1. On the Dashboard or Home page, click **Build Context** to load users and extensions
2. Navigate to the **Patch Plan** page
3. Select which issue categories you want to fix using the checkboxes:
   - ☑️ Missing Extensions
   - ☑️ Duplicate Users
   - ☑️ Discrepancies
   - ☐ Reassert Consistent (optional, typically left unchecked)
4. Click **Generate Plan** to see what would be patched

### Step 2: Review the Plan

The plan shows:
- **Category**: Which issue type each item belongs to
- **Action**: What action will be taken (ReassertExisting, AssignSpecific, ClearExtension)
- **User**: The user being modified
- **Current Ext**: The extension currently in the user's profile
- **Target Ext**: The extension that will be assigned (or "(cleared)" if being removed)
- **Notes**: Explanation of why this action is recommended

### Step 3: Configure Patch Options

- **WhatIf**: When checked (default), shows what would happen without making real changes
- **Sleep (ms)**: Delay between API calls (default 150ms to respect rate limits)
- **Max updates**: Maximum number of users to update (0 = unlimited)
- **Max failures**: Stop after this many failures (0 = unlimited)

### Step 4: Execute with Double Verification

#### For WhatIf (Simulation) Mode:
1. Keep **WhatIf** checked
2. Click **⚠️ RUN PATCH**
3. Review the results - no actual changes will be made

#### For Real Changes:
1. **Uncheck WhatIf**
2. Type `PATCH` in the **Confirm** field
3. Click **⚠️ RUN PATCH**
4. **First Confirmation Dialog** appears:
   - Reviews the summary of changes
   - Button: "YES, CONTINUE" or "Cancel"
5. **Second Confirmation Dialog** appears:
   - Final warning that this cannot be undone automatically
   - Shows count of users being modified
   - Button: "PATCH NOW" or "Cancel"
6. If confirmed twice, the patch executes

### Step 5: Review Results

After execution, review:
- **Updated**: Users successfully patched
- **Skipped**: Users skipped (e.g., due to MaxUpdates limit)
- **Failed**: Users that failed to patch (with error messages)

Results are automatically exported to a timestamped folder.

## Selective Patching Examples

### Example 1: Fix Only Missing Extensions

1. Check only **☑️ Missing Extensions**
2. Uncheck Duplicate Users, Discrepancies, and Reassert Consistent
3. Generate Plan and Execute
4. Only users with missing extension records will be patched

### Example 2: Fix Duplicates and Discrepancies

1. Check **☑️ Duplicate Users** and **☑️ Discrepancies**
2. Uncheck Missing Extensions and Reassert Consistent
3. Generate Plan and Execute
4. Duplicate user assignments will be resolved and discrepancies will be reasserted

### Example 3: Fix Everything Except Reassert

1. Check **☑️ Missing Extensions**, **☑️ Duplicate Users**, **☑️ Discrepancies**
2. Leave **☐ Reassert Consistent** unchecked
3. Generate Plan and Execute
4. All problematic assignments will be fixed, but correct ones won't be touched

## Safety Features

### Double Verification
- Two confirmation dialogs must be approved when running real changes
- Prevents accidental execution of mass updates
- Each dialog clearly warns about the consequences

### WhatIf Mode
- Default mode is WhatIf (simulation)
- Shows exactly what would happen without making changes
- Use this to review before running for real

### Confirmation Text
- Must type `PATCH` exactly (case-sensitive) in the confirm field
- Extra safety measure to ensure intentional execution

### Rate Limiting
- Configurable sleep between API calls (default 150ms)
- Prevents overwhelming the Genesys Cloud API
- Respects rate limits

### Max Limits
- MaxUpdates: Stop after updating N users
- MaxFailures: Stop after N failures
- Allows testing on small batches first

## Action Types Explained

### ReassertExisting
- Reads the current extension from the user profile
- Patches it back with a version bump
- Used for: Discrepancies, Reassert Consistent
- Goal: Sync the user profile with the telephony system

### AssignSpecific
- Assigns a specific new extension number
- Used for: Duplicate Users, Missing Extensions (when available extensions exist)
- Goal: Give the user a valid, unique extension

### ClearExtension
- Sets the extension field to null/blank
- Used for: Duplicate Users, Missing Extensions (when no available extensions)
- Goal: Remove invalid extension assignments

## Best Practices

1. **Always start with WhatIf mode** to preview changes
2. **Fix one category at a time** if you're unsure
3. **Use MaxUpdates** to test on a small batch first (e.g., MaxUpdates=5)
4. **Review the plan carefully** before executing
5. **Check the export folder** after patching to see detailed results
6. **Keep the confirmation text field clear** when not ready to patch

## Troubleshooting

### "Context not built or plan not generated"
- Go to Home/Dashboard and click **Build Context** first
- Then return to Patch Plan and click **Generate Plan**

### "To run real changes: uncheck WhatIf and type PATCH"
- Uncheck the WhatIf checkbox
- Type exactly `PATCH` in the Confirm field (case-sensitive)

### Patch fails with "MaxFailuresReached"
- Some users failed to patch
- Check the Failed list for error messages
- Adjust MaxFailures or fix the underlying issues

### No items in plan
- Check that at least one category checkbox is selected
- Verify that the audit actually found issues in those categories
- Generate Plan again after changing selections

## Output

After patching, results are exported to:
- Windows: `Documents\GcExtensionAudit\yyyyMMdd_HHmmss\`

Exports include:
- `PatchUpdated.csv` - Successfully patched users
- `PatchSkipped.csv` - Skipped users with reasons
- `PatchFailed.csv` - Failed users with error messages
- `PatchSummary.csv` - Summary statistics
- `Snapshot.json` - Full context and API stats
