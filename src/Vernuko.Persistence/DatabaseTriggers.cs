namespace Vernuko.Persistence;

/// <summary>
/// Database-level invariants, independent of application code:
/// 1) no two active exclusive bookings of the same staff/room/device may overlap;
/// 2) financial transactions, session ledger and audit log are append-only (no UPDATE/DELETE).
/// </summary>
public static class DatabaseTriggers
{
    public const string OverlapError = "VERNUKO_BOOKING_OVERLAP";
    public const string AppendOnlyError = "VERNUKO_APPEND_ONLY";

    private const string OverlapCondition = """
        EXISTS (
            SELECT 1 FROM "AppointmentBookings" b
            WHERE b."ResourceId" = NEW."ResourceId"
              AND b."IsActive" = 1 AND b."IsExclusive" = 1
              AND b."Id" <> NEW."Id"
              AND b."AppointmentId" <> NEW."AppointmentId"
              AND b."BlockStartUtc" < NEW."BlockEndUtc"
              AND NEW."BlockStartUtc" < b."BlockEndUtc")
        """;

    public static IEnumerable<string> CreateStatements()
    {
        yield return $"""
            CREATE TRIGGER "trg_AppointmentBookings_NoOverlap_Insert"
            BEFORE INSERT ON "AppointmentBookings"
            WHEN NEW."IsActive" = 1 AND NEW."IsExclusive" = 1 AND {OverlapCondition}
            BEGIN
                SELECT RAISE(ABORT, '{OverlapError}');
            END;
            """;
        yield return $"""
            CREATE TRIGGER "trg_AppointmentBookings_NoOverlap_Update"
            BEFORE UPDATE OF "IsActive", "BlockStartUtc", "BlockEndUtc", "ResourceId", "IsExclusive" ON "AppointmentBookings"
            WHEN NEW."IsActive" = 1 AND NEW."IsExclusive" = 1 AND {OverlapCondition}
            BEGIN
                SELECT RAISE(ABORT, '{OverlapError}');
            END;
            """;

        foreach (var table in new[] { "CustomerTransactions", "SessionLedger", "AuditEntries" })
        {
            yield return $"""
                CREATE TRIGGER "trg_{table}_NoUpdate" BEFORE UPDATE ON "{table}"
                BEGIN
                    SELECT RAISE(ABORT, '{AppendOnlyError}');
                END;
                """;
            yield return $"""
                CREATE TRIGGER "trg_{table}_NoDelete" BEFORE DELETE ON "{table}"
                BEGIN
                    SELECT RAISE(ABORT, '{AppendOnlyError}');
                END;
                """;
        }
    }

    public static IEnumerable<string> DropStatements()
    {
        yield return "DROP TRIGGER IF EXISTS \"trg_AppointmentBookings_NoOverlap_Insert\";";
        yield return "DROP TRIGGER IF EXISTS \"trg_AppointmentBookings_NoOverlap_Update\";";
        foreach (var table in new[] { "CustomerTransactions", "SessionLedger", "AuditEntries" })
        {
            yield return $"DROP TRIGGER IF EXISTS \"trg_{table}_NoUpdate\";";
            yield return $"DROP TRIGGER IF EXISTS \"trg_{table}_NoDelete\";";
        }
    }
}
