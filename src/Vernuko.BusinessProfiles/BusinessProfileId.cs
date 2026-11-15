namespace Vernuko.BusinessProfiles;

/// <summary>
/// How the program works for a business, chosen at setup with the profile and fixed afterwards: each mode is sold
/// with its own license (the license names the mode's product). The other modes' screens a business needs
/// (stock and sales for a repair shop) are open inside its mode.
/// </summary>
public enum BusinessMode
{
    /// <summary>Vernuko Randevu: appointments, packages, staff calendar.</summary>
    Randevu,

    /// <summary>Vernuko Stok: barcode sales, purchases, suppliers, stock counts, current accounts.</summary>
    Stok,

    /// <summary>Vernuko Teknik Servis: service tickets, device history, parts, approvals, delivery.</summary>
    TeknikServis,
}

/// <summary>Business verticals. Stored by name. Beauty and laser are deliberately ONE profile.</summary>
public enum BusinessProfileId
{
    BeautyAndLaser,
    HairAndBarber,
    SpaAndMassage,
    MedicalClinic,
    DentalClinic,
    Psychology,
    Veterinary,
    FitnessAndPilates,
    LawFirm,
    Education,
    ConsultingAndAccounting,
    AutoService,
    PhotoAndEvents,
    General,

    // Stok mode (appended: profiles are stored by name, new ones go last)
    HardwareStore,
    Stationery,
    AutoParts,
    Wholesale,
    RetailStore,

    // Teknik Servis mode
    PhoneRepair,
    ComputerRepair,
    ApplianceRepair,
    ElectronicsRepair,
}

/// <summary>Optional capabilities switched on per profile (and later per business).</summary>
public enum ProfileFeature
{
    SessionPackages,
    BodyAreas,
    Rooms,
    Devices,
    Chairs,
    TreatmentUnits,
    ServiceBays,
    Studios,
    Equipment,
    PrepAndCleanupBuffers,
    MultiServiceAppointments,
    MultiStaffServices,
    ProcessingTimes,
    ProductStock,
    Commissions,
    Deposits,
    ConsentForms,
    CustomerPhotos,
    GiftCards,
    Memberships,
    GroupClasses,
    Capacity,
    RecurringAppointments,
    Waitlist,
    QueueNumbers,
    PrivateNotes,
    OnlineMeetingLinks,
    Pets,
    Vehicles,
    Guardians,
    Attendance,
    MakeupLessons,
    CompanyClients,
    DocumentChecklists,
    ConflictOfInterestCheck,
    DeliveryDates,
    MaintenanceReminders,

    /// <summary>Counter sales with barcode (satış ekranı).</summary>
    Sales,

    /// <summary>Goods receipts from suppliers and supplier accounts (alış, tedarikçi carisi).</summary>
    Purchases,

    /// <summary>Repair intake and tracking (servis fişi).</summary>
    ServiceTickets,
}

/// <summary>How sensitive the typical data of this vertical is; drives defaults for messaging and access.</summary>
public enum DataSensitivity
{
    Standard,
    Confidential,
    SpecialCategory,
}
