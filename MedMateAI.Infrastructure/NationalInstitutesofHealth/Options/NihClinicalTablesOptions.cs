namespace MedMateAI.Infrastructure.NationalInstitutesofHealth.Options;

public sealed class NihClinicalTablesOptions
{
    public const string SectionName = "NihClinicalTables";

    public string BaseUrl { get; set; } = "https://clinicaltables.nlm.nih.gov/api/icd10cm/v3";
}
