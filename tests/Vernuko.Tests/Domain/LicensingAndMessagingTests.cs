using System.Security.Cryptography;
using Vernuko.Licensing;
using Vernuko.Messaging;

namespace Vernuko.Tests.Domain;

public class LicensingAndMessagingTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 7, 0, 0, DateTimeKind.Utc);

    public static LicenseDocument Doc(DateTime validUntil, DateTime refreshAfter, string device = "dev-1") => new()
    {
        LicenseId = "LIC-1",
        Product = "vernuko-desktop",
        Edition = "Standart",
        ActivationId = "ACT-1",
        DeviceHash = device,
        IssuedAtUtc = Now,
        ValidUntilUtc = validUntil,
        RefreshAfterUtc = refreshAfter,
        OfflineGraceDays = 14,
    };

    [Fact]
    public void Signed_license_verifies_and_tampering_fails()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var verifier = new LicenseVerifier(new Dictionary<string, byte[]> { ["k1"] = key.ExportSubjectPublicKeyInfo() });
        var signed = LicenseVerifier.Sign(Doc(Now.AddDays(30), Now.AddDays(7)), key, "k1");
        Assert.True(verifier.Verify(signed).IsSuccess);

        var envelope = System.Text.Json.JsonSerializer.Deserialize<SignedLicense>(signed)!;
        var forgedPayload = Base64Url.Encode(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Doc(Now.AddYears(10), Now.AddYears(10))));
        var forged = System.Text.Json.JsonSerializer.Serialize(envelope with { Payload = forgedPayload });
        Assert.Equal(LicenseErrors.BadSignature, verifier.Verify(forged).Error);

        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Assert.Equal(LicenseErrors.UnknownKey, verifier.Verify(LicenseVerifier.Sign(Doc(Now, Now), otherKey, "other")).Error);
        Assert.Equal(LicenseErrors.BadSignature, verifier.Verify(LicenseVerifier.Sign(Doc(Now, Now), otherKey, "k1")).Error);
    }

    [Fact]
    public void Offline_use_works_until_grace_ends_then_read_only()
    {
        var local = new LicenseLocalState(Now.AddDays(-100), Now, "signed");
        var doc = Doc(Now.AddDays(60), Now.AddDays(7));
        Assert.Equal(LicenseState.Active, LicenseEvaluator.Evaluate(local, doc, "dev-1", Now).State);
        var grace = LicenseEvaluator.Evaluate(local, doc, "dev-1", Now.AddDays(10));
        Assert.Equal(LicenseState.OfflineGrace, grace.State);
        Assert.True(grace.CanWrite);
        var expired = LicenseEvaluator.Evaluate(local, doc, "dev-1", Now.AddDays(22));
        Assert.Equal(AccessMode.ReadOnly, expired.Mode);
    }

    [Fact]
    public void Subscription_end_is_read_only_never_data_loss()
    {
        var local = new LicenseLocalState(Now.AddDays(-400), Now, "signed");
        var status = LicenseEvaluator.Evaluate(local, Doc(Now.AddDays(-1), Now.AddDays(-10)), "dev-1", Now);
        Assert.Equal(LicenseState.Expired, status.State);
        Assert.Equal(AccessMode.ReadOnly, status.Mode);
        Assert.Contains("silinmedi", status.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Clock_rollback_cannot_extend_validity_but_does_not_lock_by_itself()
    {
        var doc = Doc(Now.AddDays(5), Now.AddDays(5));
        var local = new LicenseLocalState(Now.AddDays(-30), Now.AddDays(10), "signed"); // we have already seen +10 days
        var status = LicenseEvaluator.Evaluate(local, doc, "dev-1", Now); // clock moved back 10 days
        Assert.True(status.ClockRollbackSuspected);
        Assert.Equal(AccessMode.ReadOnly, status.Mode); // effective time is the high-water mark (after expiry)

        var stillValid = LicenseEvaluator.Evaluate(local with { HighWaterMarkUtc = Now.AddDays(1) }, Doc(Now.AddDays(60), Now.AddDays(30)), "dev-1", Now.AddHours(-1));
        Assert.Equal(AccessMode.Full, stillValid.Mode);
        Assert.False(stillValid.ClockRollbackSuspected); // small drift tolerated
    }

    [Fact]
    public void Device_change_requires_reactivation_but_keeps_read_access()
    {
        var local = new LicenseLocalState(Now.AddDays(-10), Now, "signed");
        var status = LicenseEvaluator.Evaluate(local, Doc(Now.AddDays(60), Now.AddDays(7)), "new-windows-install", Now);
        Assert.Equal(LicenseState.DeviceMismatch, status.State);
        Assert.Equal(AccessMode.ReadOnly, status.Mode);
    }

    [Fact]
    public void Trial_counts_down_then_read_only()
    {
        var local = new LicenseLocalState(Now, Now, null);
        Assert.Equal(30, LicenseEvaluator.Evaluate(local, null, "d", Now).DaysLeft);
        Assert.Equal(AccessMode.ReadOnly, LicenseEvaluator.Evaluate(local, null, "d", Now.AddDays(31)).Mode);
    }

    [Fact]
    public void WhatsApp_link_uses_official_click_to_chat_format()
    {
        var phone = Vernuko.Core.PhoneNumber.Parse("0532 456 78 90").Value;
        var link = WhatsAppClickToChat.BuildLink(phone, "Merhaba Ayşe, randevunuz 14:00");
        Assert.Equal("https://wa.me/905324567890?text=Merhaba%20Ay%C5%9Fe%2C%20randevunuz%2014%3A00", link.AbsoluteUri);
    }

    [Fact]
    public void Templates_hide_service_name_for_special_category_profiles()
    {
        var ctx = new MessageContext("Ayşe", "Klinik", new DateTime(2026, 11, 12, 14, 0, 0), "Psikoterapi", "Dr. X", "0212");
        var tpl = "Merhaba {Ad}, {Tarih} {Saat} {Hizmet} randevunuz var. {Bilinmeyen}";
        var hidden = MessageTemplates.Render(tpl, ctx, hideServiceName: true);
        Assert.DoesNotContain("Psikoterapi", hidden, StringComparison.Ordinal);
        Assert.Contains("12 Kasım 2026 Perşembe 14:00", hidden, StringComparison.Ordinal);
        Assert.Contains("{Bilinmeyen}", hidden, StringComparison.Ordinal);
        Assert.Contains("Psikoterapi", MessageTemplates.Render(tpl, ctx, hideServiceName: false), StringComparison.Ordinal);
    }
}
