using System.Collections.Generic;
using GcExtensionAuditMaui.Models.Api;
using GcExtensionAuditMaui.Models.Audit;
using GcExtensionAuditMaui.Services;
using Xunit;

namespace GcExtensionAuditMaui.Tests;

public sealed class ReportModuleTests
{
    [Fact]
    public void BuildApiSnapshots_UsesCorrectSheetName_ForExtensions()
    {
        // Arrange
        var context = CreateTestContext(AuditNumberKind.Extension);
        
        // Act
        var snapshots = InvokePrivateMethod_BuildApiSnapshots(context);
        
        // Assert
        var extensionSnapshot = Assert.Single(snapshots, s => s.SheetName == "Extensions");
        Assert.NotNull(extensionSnapshot);
        Assert.Equal("/api/v2/telephony/providers/edges/extensions", extensionSnapshot.Endpoint);
    }

    [Fact]
    public void BuildApiSnapshots_UsesCorrectSheetName_ForDIDs()
    {
        // Arrange
        var context = CreateTestContext(AuditNumberKind.Did);
        
        // Act
        var snapshots = InvokePrivateMethod_BuildApiSnapshots(context);
        
        // Assert
        var didSnapshot = Assert.Single(snapshots, s => s.SheetName == "DIDs");
        Assert.NotNull(didSnapshot);
        Assert.Equal("/api/v2/telephony/providers/edges/dids", didSnapshot.Endpoint);
    }

    [Fact]
    public void ConvertReportToIssues_UsesCorrectEntityType_ForDIDs()
    {
        // Arrange
        var context = CreateTestContext(AuditNumberKind.Did);
        var report = CreateTestReport();
        
        // Act
        var issues = InvokePrivateMethod_ConvertReportToIssues(context, report);
        
        // Assert
        // Check that DID-related issues use "DID" as EntityType
        var didIssue = Assert.Single(issues, i => i.EntityType == "DID");
        Assert.Contains("DID", didIssue.IssueFound);
    }

    [Fact]
    public void ConvertReportToIssues_UsesCorrectEntityType_ForExtensions()
    {
        // Arrange
        var context = CreateTestContext(AuditNumberKind.Extension);
        var report = CreateTestReport();
        
        // Act
        var issues = InvokePrivateMethod_ConvertReportToIssues(context, report);
        
        // Assert
        // Check that Extension-related issues use "Extension" as EntityType
        var extIssue = Assert.Single(issues, i => i.EntityType == "Extension");
        Assert.Contains("Extension", extIssue.IssueFound);
    }

    private static AuditContext CreateTestContext(AuditNumberKind kind)
    {
        var extensions = new List<GcExtension>
        {
            new() { Id = "e1", Number = "100", OwnerType = "USER", Owner = new GcExtensionOwner { Id = "u1" } }
        };

        var extByNumber = new Dictionary<string, IReadOnlyList<GcExtension>>(StringComparer.OrdinalIgnoreCase)
        {
            ["100"] = new List<GcExtension> { extensions[0] }
        };

        return new AuditContext
        {
            AuditKind = kind,
            ApiBaseUri = "https://api.usw2.pure.cloud",
            AccessToken = "token",
            IncludeInactive = false,
            Users = new List<GcUser>(),
            UsersById = new Dictionary<string, GcUser>(StringComparer.OrdinalIgnoreCase),
            UserDisplayById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            UsersWithProfileExtension = new List<UserWithProfileExtensionRow>(),
            ProfileExtensionNumbers = new List<string>(),
            Extensions = extensions,
            ExtensionMode = "FULL",
            ExtensionCache = null,
            ExtensionsByNumber = extByNumber,
        };
    }

    private static DryRunReport CreateTestReport()
    {
        return new DryRunReport
        {
            Metadata = new DryRunMetadata
            {
                GeneratedAt = "2024-01-01T00:00:00Z",
                ApiBaseUri = "https://api.usw2.pure.cloud",
                ExtensionMode = "FULL",
                UsersTotal = 1,
                UsersWithProfileExtension = 0,
                DistinctProfileExtensions = 0,
                ExtensionsLoaded = 1
            },
            Rows = new List<DryRunRow>(),
            Summary = new DryRunSummary
            {
                TotalRows = 0,
                MissingAssignments = 0,
                Discrepancies = 0,
                DuplicateUserRows = 0,
                DuplicateExtensionRows = 1,
                UserIssues = 0
            },
            MissingAssignments = new List<MissingAssignmentRow>(),
            Discrepancies = new List<DiscrepancyRow>(),
            DuplicateUserAssignments = new List<DuplicateUserAssignmentRow>(),
            DuplicateExtensionRecords = new List<DuplicateExtensionRecordRow>
            {
                new() 
                { 
                    ExtensionId = "e1", 
                    ExtensionNumber = "100", 
                    OwnerType = "USER", 
                    OwnerId = "u1", 
                    ExtensionPoolId = null 
                }
            },
            UserIssues = new List<UserIssueRow>()
        };
    }

    // Helper to invoke private BuildApiSnapshots method using reflection
    private static List<ApiSnapshot> InvokePrivateMethod_BuildApiSnapshots(AuditContext context)
    {
        var method = typeof(ReportModule).GetMethod("BuildApiSnapshots", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = method!.Invoke(null, new object[] { context });
        return (List<ApiSnapshot>)result!;
    }

    // Helper to invoke private ConvertReportToIssues method using reflection
    private static List<IssueRow> InvokePrivateMethod_ConvertReportToIssues(AuditContext context, DryRunReport report)
    {
        var method = typeof(ReportModule).GetMethod("ConvertReportToIssues", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = method!.Invoke(null, new object[] { context, report });
        return (List<IssueRow>)result!;
    }
}
