namespace Vernuko.BusinessProfiles;

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
}

/// <summary>How sensitive the typical data of this vertical is; drives defaults for messaging and access.</summary>
public enum DataSensitivity
{
    Standard,
    Confidential,
    SpecialCategory,
}
