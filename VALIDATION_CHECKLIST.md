# Validation Checklist

## Automated Tests
- [ ] Run existing unit tests: `dotnet test tests/GcExtensionAuditMaui.Tests/GcExtensionAuditMaui.Tests.csproj`
- [ ] Verify all existing tests pass (no regressions)
- [ ] Verify new ReportModuleTests pass

## Manual UI Testing

### Single Audit Mode (Existing Functionality)
- [ ] Extension audit works as before
  - [ ] "Ext" radio button selects Extension mode
  - [ ] Title shows "Genesys Audits"
  - [ ] Build Context fetches users and extensions
  - [ ] Export creates file named "GenesysExtensionAudit_*.xlsx"
  - [ ] Excel has "Users" and "Extensions" sheets
  - [ ] Issues labeled as "Extension" EntityType

- [ ] DID audit works correctly
  - [ ] "DID" radio button selects DID mode
  - [ ] Title shows "Genesys Cloud DID Audit"
  - [ ] Build Context fetches users and DIDs
  - [ ] Export creates file named "GenesysDIDAudit_*.xlsx"
  - [ ] Excel has "Users" and "DIDs" sheets (NOT "Extensions")
  - [ ] Issues labeled as "DID" EntityType (NOT "Extension")

### Combined Audit Mode (New Feature)
- [ ] "Both" checkbox enables combined mode
  - [ ] Checking "Both" updates title to "Genesys Cloud Combined Audit (Extensions + DIDs)"
  - [ ] Build Context shows progress for both Extension and DID contexts
  - [ ] Export creates file named "GenesysCombinedAudit_*.xlsx"
  - [ ] Excel has "Users", "Extensions", and "DIDs" sheets
  - [ ] "Issues" sheet contains both Extension and DID issues
  - [ ] Extension issues have EntityType="Extension"
  - [ ] DID issues have EntityType="DID"

## Data Validation

### Excel Export Structure
- [ ] "Users" sheet has flattened JSON columns (e.g., addresses.extension, addresses.type)
- [ ] "Extensions" sheet has flattened JSON columns (e.g., owner.id, owner.name)
- [ ] "DIDs" sheet has flattened JSON columns (e.g., owner.id, phoneNumber)
- [ ] "Issues" sheet has all required columns: IssueFound, CurrentState, NewState, Severity, EntityType, etc.

### Issue Labeling Verification
Check that issue messages use correct terminology:

**DID Audit Issues**:
- [ ] Discrepancies: "DID {number} has issue" (not "Extension")
- [ ] Missing: "DID needs to be created" (not "Extension")
- [ ] Duplicates: "Duplicate DID Assignment" (not "Extension")
- [ ] EntityType field shows "DID" (not "Extension")

**Extension Audit Issues**:
- [ ] Discrepancies: "Extension {number} has issue"
- [ ] Missing: "Extension needs to be created"
- [ ] Duplicates: "Duplicate Extension Assignment"
- [ ] EntityType field shows "Extension"

## Performance Validation

### API Call Minimization
When running combined audit, verify:
- [ ] Users API called only once (check logs)
- [ ] Extensions API called once
- [ ] DIDs API called once
- [ ] Total: 3 API calls (not 6)

## Backward Compatibility
- [ ] Existing single audits work without checking "Both"
- [ ] Preferences are saved/restored correctly
- [ ] No breaking changes to existing workflows

## Edge Cases
- [ ] Empty user list handled gracefully
- [ ] Empty extension list handled gracefully
- [ ] Empty DID list handled gracefully
- [ ] Large datasets (1000+ users) export successfully
- [ ] Cancel during build context works correctly
- [ ] Export with no issues works correctly
