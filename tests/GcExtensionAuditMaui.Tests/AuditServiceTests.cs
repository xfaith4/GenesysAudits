using System.Net.Http;
using GcExtensionAuditMaui.Models.Api;
using GcExtensionAuditMaui.Models.Audit;
using GcExtensionAuditMaui.Models.Observability;
using GcExtensionAuditMaui.Models.Patch;
using GcExtensionAuditMaui.Models.Planning;
using GcExtensionAuditMaui.Services;

namespace GcExtensionAuditMaui.Tests;

public sealed class AuditServiceTests
{
    private static AuditService CreateAuditService()
    {
        var log = new LoggingService();
        var api = new GenesysCloudApiClient(new HttpClient(), new ApiStats(), log);
        return new AuditService(api, log);
    }

    private static AuditContext CreateContext()
    {
        var users = new List<GcUser>
        {
            new()
            {
                Id = "u1",
                Name = "Alice",
                Email = "alice@example.com",
                State = "active",
                Addresses = new List<GcUserAddress> { new() { MediaType = "PHONE", Type = "WORK", Extension = "100" } },
            },
            new()
            {
                Id = "u2",
                Name = "Bob",
                Email = "bob@example.com",
                State = "active",
                Addresses = new List<GcUserAddress> { new() { MediaType = "PHONE", Type = "WORK", Extension = "100" } },
            },
            new()
            {
                Id = "u3",
                Name = "Carol",
                Email = "carol@example.com",
                State = "active",
                Addresses = new List<GcUserAddress> { new() { MediaType = "PHONE", Type = "WORK", Extension = "200" } },
            },
            new()
            {
                Id = "u4",
                Name = "Dan",
                Email = "dan@example.com",
                State = "active",
                Addresses = new List<GcUserAddress> { new() { MediaType = "PHONE", Type = "WORK", Extension = "300" } },
            },
            new()
            {
                Id = "u5",
                Name = "Eve",
                Email = "eve@example.com",
                State = "active",
                Addresses = new List<GcUserAddress> { new() { MediaType = "PHONE", Type = "WORK", Extension = "400" } },
            },
            new()
            {
                Id = "u6",
                Name = "Frank",
                Email = "frank@example.com",
                State = "active",
                Addresses = new List<GcUserAddress> { new() { MediaType = "PHONE", Type = "WORK", Extension = "500" } },
            },
        };

        var userById = users.Where(u => u.Id is not null).ToDictionary(u => u.Id!, StringComparer.OrdinalIgnoreCase);
        var userDisplayById = users.Where(u => u.Id is not null).ToDictionary(u => u.Id!, u => $"{u.Name} <{u.Email}>", StringComparer.OrdinalIgnoreCase);

        var usersWithProfileExt = users.Select(u => new UserWithProfileExtensionRow
        {
            UserId = u.Id!,
            UserName = u.Name,
            UserEmail = u.Email,
            UserState = u.State,
            ProfileExtension = AuditService.GetUserProfileExtension(u)!,
        }).ToList();

        var profileExtNumbers = usersWithProfileExt.Select(r => r.ProfileExtension).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var extensions = new List<GcExtension>
        {
            // duplicates extensions: number 200
            new() { Id = "e200-1", Number = "200", OwnerType = "USER", Owner = new GcExtensionOwner { Id = "u3" }, ExtensionPool = new GcExtensionPool { Id = "p1" } },
            new() { Id = "e200-2", Number = "200", OwnerType = "USER", Owner = new GcExtensionOwner { Id = "u3" }, ExtensionPool = new GcExtensionPool { Id = "p1" } },

            // owner mismatch for u4: extension 300 owned by u1
            new() { Id = "e300", Number = "300", OwnerType = "USER", Owner = new GcExtensionOwner { Id = "u1" }, ExtensionPool = new GcExtensionPool { Id = "p1" } },

            // owner type not user for u5
            new() { Id = "e400", Number = "400", OwnerType = "QUEUE", Owner = new GcExtensionOwner { Id = "q1" }, ExtensionPool = new GcExtensionPool { Id = "p1" } },
        };

        var extByNumber = extensions
            .Where(e => !string.IsNullOrWhiteSpace(e.Number))
            .GroupBy(e => e.Number!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<GcExtension>)g.ToList(), StringComparer.OrdinalIgnoreCase);

        return new AuditContext
        {
            AuditKind = AuditNumberKind.Extension,
            ApiBaseUri = "https://api.usw2.pure.cloud",
            AccessToken = "token",
            IncludeInactive = false,
            Users = users,
            UsersById = userById,
            UserDisplayById = userDisplayById,
            UsersWithProfileExtension = usersWithProfileExt,
            ProfileExtensionNumbers = profileExtNumbers,
            Extensions = extensions,
            ExtensionMode = "FULL",
            ExtensionCache = null,
            ExtensionsByNumber = extByNumber,
        };
    }

    [Fact]
    public void DuplicateUsers_AreDetected()
    {
        var svc = CreateAuditService();
        var ctx = CreateContext();

        var dups = svc.FindDuplicateUserExtensionAssignments(ctx);
        Assert.Equal(2, dups.Count);
        Assert.All(dups, d => Assert.Equal("100", d.ProfileExtension));
    }

    [Fact]
    public void DuplicateExtensions_AreDetected()
    {
        var svc = CreateAuditService();
        var ctx = CreateContext();

        var dups = svc.FindDuplicateExtensionRecords(ctx);
        Assert.Equal(2, dups.Count);
        Assert.All(dups, d => Assert.Equal("200", d.ExtensionNumber));
    }

    [Fact]
    public void Discrepancies_ExcludeDuplicates_AndIncludeOwnerIssues()
    {
        var svc = CreateAuditService();
        var ctx = CreateContext();

        var rows = svc.FindExtensionDiscrepancies(ctx);
        Assert.Equal(2, rows.Count);

        Assert.Contains(rows, r => r.Issue == "OwnerMismatch" && r.ProfileExtension == "300" && r.UserId == "u4");
        Assert.Contains(rows, r => r.Issue == "OwnerTypeNotUser" && r.ProfileExtension == "400" && r.UserId == "u5");
    }

    [Fact]
    public void MissingAssignments_ExcludeDuplicates_AndIncludeOnlyMissing()
    {
        var svc = CreateAuditService();
        var ctx = CreateContext();

        var rows = svc.FindMissingExtensionAssignments(ctx);
        Assert.Single(rows);
        Assert.Equal("NoExtensionRecord", rows[0].Issue);
        Assert.Equal("500", rows[0].ProfileExtension);
        Assert.Equal("u6", rows[0].UserId);
    }

    [Fact]
    public void DryRunReport_ContainsExpectedRows()
    {
        var svc = CreateAuditService();
        var ctx = CreateContext();

        var report = svc.NewDryRunReport(ctx);
        Assert.Equal(7, report.Rows.Count);
        Assert.Equal(1, report.Summary.MissingAssignments);
        Assert.Equal(2, report.Summary.Discrepancies);
        Assert.Equal(2, report.Summary.DuplicateUserRows);
        Assert.Equal(2, report.Summary.DuplicateExtensionRows);

        var mismatch = report.Rows.Single(r => r.Category == "OwnerMismatch");
        Assert.Equal("Alice <alice@example.com>", mismatch.Before_ExtOwner);
    }

    [Fact]
    public void UserProfileDid_IsExtracted_FromPhoneAddress()
    {
        var user = new GcUser
        {
            Id = "u1",
            Name = "Alice",
            Email = "alice@example.com",
            Addresses = new List<GcUserAddress>
            {
                new()
                {
                    MediaType = "PHONE",
                    Type = "WORK",
                    Extra = new Dictionary<string, object?>
                    {
                        ["address"] = "+1 (317) 555-1212",
                    },
                },
            },
        };

        var did = AuditService.GetUserProfileDid(user);
        Assert.Equal("+13175551212", did);
    }

    [Fact]
    public void PatchFromPlanOptions_FiltersCategories_Correctly()
    {
        var svc = CreateAuditService();
        var ctx = CreateContext();
        
        var planner = new FixupPlannerService(svc);
        var plan = planner.BuildPlan(ctx, reassertConsistentUsers: false, preferAssignAvailableOverBlank: true);

        // Verify plan has items from different categories
        Assert.Contains(plan.Items, i => i.Category == "Missing");
        Assert.Contains(plan.Items, i => i.Category == "DuplicateUser");
        Assert.Contains(plan.Items, i => i.Category == "Discrepancy");
        
        // Test filtering - only Missing
        var missingOnly = plan.Items.Where(item => item.Category == "Missing").ToList();
        Assert.Single(missingOnly);
        Assert.Equal("Missing", missingOnly[0].Category);
        
        // Test filtering - only DuplicateUser
        var duplicateOnly = plan.Items.Where(item => item.Category == "DuplicateUser").ToList();
        Assert.Single(duplicateOnly); // Bob is duplicate of Alice on ext 100
        Assert.Equal("DuplicateUser", duplicateOnly[0].Category);
        
        // Test filtering - only Discrepancy
        var discrepancyOnly = plan.Items.Where(item => item.Category == "Discrepancy").ToList();
        Assert.Equal(2, discrepancyOnly.Count); // u4 owner mismatch, u5 owner type not user
        Assert.All(discrepancyOnly, d => Assert.Equal("Discrepancy", d.Category));
    }

    [Fact]
    public void PatchFromPlanOptions_RespectsIncludeFlags()
    {
        // Verify that PatchFromPlanOptions properly filters based on flags
        var options1 = new PatchFromPlanOptions
        {
            WhatIf = true,
            IncludeMissing = true,
            IncludeDuplicateUser = false,
            IncludeDiscrepancy = false,
            IncludeReassert = false,
        };
        
        Assert.True(options1.IncludeMissing);
        Assert.False(options1.IncludeDuplicateUser);
        Assert.False(options1.IncludeDiscrepancy);
        Assert.False(options1.IncludeReassert);
        
        var options2 = new PatchFromPlanOptions
        {
            WhatIf = true,
            IncludeMissing = true,
            IncludeDuplicateUser = true,
            IncludeDiscrepancy = true,
            IncludeReassert = false,
        };
        
        Assert.True(options2.IncludeMissing);
        Assert.True(options2.IncludeDuplicateUser);
        Assert.True(options2.IncludeDiscrepancy);
        Assert.False(options2.IncludeReassert);
    }

    [Fact]
    public void FixupPlan_HasCorrectActionTypes()
    {
        var svc = CreateAuditService();
        var ctx = CreateContext();
        
        var planner = new FixupPlannerService(svc);
        var plan = planner.BuildPlan(ctx, reassertConsistentUsers: false, preferAssignAvailableOverBlank: true);

        // Missing item should have ClearExtension action (no available extensions in test context)
        var missing = plan.Items.FirstOrDefault(i => i.Category == "Missing");
        Assert.NotNull(missing);
        Assert.Equal(FixupActionType.ClearExtension, missing.Action);
        
        // Discrepancy items should have ReassertExisting action
        var discrepancies = plan.Items.Where(i => i.Category == "Discrepancy").ToList();
        Assert.All(discrepancies, d => Assert.Equal(FixupActionType.ReassertExisting, d.Action));
    }
}
