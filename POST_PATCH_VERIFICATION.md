# Post-Patch Verification Feature

## Overview

The post-patch verification feature provides automatic validation that patch operations were successfully applied. After patching user extensions, the system re-fetches each user and compares the actual state against the expected state.

## How It Works

### Verification Process

1. **Patch Execution**: User extensions are patched via the API
2. **Automatic Re-fetch**: Each patched user is retrieved from the API
3. **State Comparison**: The actual extension is compared against the expected value
4. **Status Determination**:
   - ✓ **Confirmed**: Extension matches expected value
   - ⚠️ **Mismatch**: Extension doesn't match (potential sync issue)
   - ❌ **UserNotFound**: User could not be retrieved
   - ❌ **Error**: Other error occurred during verification

### When Verification Runs

- **Only for REAL patches** (WhatIf = false)
- **Only when enabled** (checkbox in UI)
- **Only for successful patches** (failed patches are not verified)
- **After all patches complete** (not during patching)

## Using Post-Patch Verification

### In the UI

1. Navigate to **Patch Plan** page
2. Look for the checkbox: "Enable post-patch verification"
3. **Default**: Enabled (recommended)
4. **When to disable**:
   - Large batch operations where verification would take too long
   - When you trust the API responses and don't need validation
   - Testing scenarios where verification is not needed

### Verification Results Display

After patching completes, the verification results appear in a green frame:

**Summary Line**:
```
✓ Verified 25 patches: 24 confirmed, 1 mismatched, 0 users not found
```

**Details Table**:
- User name and ID
- Expected extension
- Actual extension (color-coded)
- Status (Confirmed/Mismatch/Error)
- Notes (for mismatches/errors)

### Status Indicators

**Green (Confirmed)**:
- Extension value matches exactly
- Cleared extensions (both expected and actual are null/empty)

**Red (Mismatch)**:
- Extension value doesn't match expected
- One is cleared but the other has a value
- Indicates potential API delay or sync issue

**Gray (UserNotFound/Error)**:
- User could not be retrieved during verification
- May indicate user was deleted or access issue

## What To Do About Mismatches

### Common Causes

1. **API Propagation Delay**: Changes haven't fully propagated yet
   - Wait a few minutes and run verification again (future feature)
   
2. **Concurrent Changes**: Another process modified the user
   - Check user audit logs in Genesys Cloud
   
3. **API Error Not Caught**: Patch appeared successful but wasn't applied
   - Review logs for error details
   - Re-run patch for affected users

### Recommended Actions

**For 1-2 Mismatches**:
- Review each case individually
- Check if a delay caused the mismatch
- Manually verify in Genesys Cloud UI
- Re-patch if needed

**For Many Mismatches**:
- Stop further operations
- Review API logs and error messages
- Check for systemic issues (API outage, permissions, etc.)
- Contact support if needed

## Technical Details

### Verification Logic

```csharp
// Simplified verification logic
var actualExtension = GetUserProfileExtension(user);
var expectedExtension = patchedRow.Extension;

bool matches = 
    (bothAreCleared) ||
    (string.Equals(expected, actual, OrdinalIgnoreCase));
```

### Performance Considerations

- **Rate Limiting**: 100ms delay between user fetches
- **API Calls**: One GET per verified user
- **Time**: Approximately 0.1 seconds per user
  - 10 users ≈ 1 second
  - 100 users ≈ 10 seconds
  - 1000 users ≈ 100 seconds (~1.5 minutes)

### Verification Export

Verification results are NOT currently exported to files. The results are:
- Displayed in the UI
- Logged to the log file
- Available in the session until cleared

**Future Enhancement**: Export verification results to CSV for audit trails.

## Configuration

### Preference Storage

The "Enable post-patch verification" setting is saved to user preferences:
- **Key**: `EnablePostPatchVerification`
- **Default**: `true`
- **Scope**: Per-user, per-machine

### Disabling Verification

To disable by default:
1. Run the app once
2. Uncheck "Enable post-patch verification"
3. The setting is saved and will persist

## Best Practices

### ✓ DO

- Keep verification enabled for production patches
- Review mismatches before running additional patches
- Use verification to build confidence in your patch process
- Log verification results for compliance/audit purposes

### ✗ DON'T

- Disable verification without good reason
- Ignore mismatches without investigation
- Assume a patch succeeded without verification
- Run large batches without checking verification results

## Troubleshooting

**"Verification Items" section doesn't appear**
- Verification only runs for REAL patches (WhatIf=false)
- Must have EnablePostPatchVerification checked
- At least one user must have been successfully patched

**All verifications show "UserNotFound"**
- Check access token permissions
- Verify users weren't deleted after patching
- Check API connectivity

**Verification takes too long**
- Expected for large batches (100ms per user)
- Consider disabling for very large operations (1000+)
- Split into smaller batches instead

**Mismatches on cleared extensions**
- Verify the patch actually set extension to null
- Check if "(cleared)" in Expected column
- Review API response logs

## Logging

Verification events are logged at these levels:

- **Info**: Verification started, completed, summary
- **Warn**: Individual mismatches detected
- **Error**: Verification errors (user not found, API errors)

Example log entries:
```
[Info] Starting post-patch verification | Count: 25
[Warn] Verification mismatch | UserId: xxx, Expected: 1234, Actual: (none)
[Info] Post-patch verification complete | Confirmed: 24, Mismatched: 1
```

## Future Enhancements

Potential improvements to this feature:

1. **Re-verification Command**: Button to re-run verification after waiting
2. **Export Verification Results**: Save to CSV for audit trail
3. **Configurable Delays**: Allow custom delay between fetches
4. **Retry on Mismatch**: Automatically retry verification once
5. **Historical Tracking**: Compare against pre-patch state
6. **Notification Alerts**: Alert on mismatch threshold exceeded

## Related Features

- **PATCH functionality**: See `PATCH_FUNCTIONALITY.md`
- **Logging**: See `README.md` logging section
- **Executive Summary**: Verification stats could be added to summary exports

## Support

For issues or questions:
- Review logs in the app's log viewer
- Check GitHub issues
- Review API documentation for user endpoints
