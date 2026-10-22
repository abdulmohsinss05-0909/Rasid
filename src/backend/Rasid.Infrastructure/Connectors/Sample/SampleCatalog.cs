namespace Rasid.Infrastructure.Connectors.Sample;

/// <summary>
/// Fictitious authors and post texts for the clearly labelled Sample connector.
/// None of these accounts exist; no real person or organisation is represented.
/// </summary>
internal static class SampleCatalog
{
    public static IReadOnlyList<(string Username, string DisplayName)> Authors { get; } =
    [
        ("sample_relief_news", "Sample Relief News"),
        ("sample_aid_watch", "Sample Aid Watch"),
        ("sample_ngo_desk", "Sample NGO Desk"),
        ("sample_field_reporter", "Sample Field Reporter"),
        ("sample_volunteer_hub", "Sample Volunteer Hub"),
        ("sample_policy_lab", "Sample Policy Lab"),
        ("sample_gulf_daily", "Sample Gulf Daily"),
        ("sample_citizen_01", "Sample Citizen 01"),
        ("sample_citizen_02", "Sample Citizen 02"),
        ("sample_dev_economist", "Sample Development Economist"),
        ("sample_health_corps", "Sample Health Corps"),
        ("sample_arabic_desk", "عينة المكتب العربي"),
    ];

    /// <summary>
    /// Texts with an optional image: a file in the web app's /sample-media folder (licensed stock photos,
    /// credited in sample-media/CREDITS.md) and its alt text.
    /// </summary>
    public static IReadOnlyList<(string Text, string Language, string? MediaFile, string? MediaAlt)> Texts { get; } =
    [
        ("New convoy of UAE aid arrives with food and medical supplies for displaced families. #UAEAid #HumanitarianAid", "en", "convoy.jpg", "Trucks lined up on a highway (sample stock photo)"),
        ("Humanitarian aid workers report faster delivery times this week thanks to new logistics hubs. #HumanitarianAid #Logistics", "en", null, null),
        ("UAE aid programme funds three new water purification plants in the region. #UAEAid #WaterForAll", "en", "water-plant.jpg", "Aerial view of a water treatment facility (sample stock photo)"),
        ("Volunteers packed 20,000 relief kits today. Proud to support humanitarian aid efforts. #HumanitarianAid #Volunteers", "en", "relief-kits.jpg", "Stacked cardboard boxes of supplies (sample stock photo)"),
        ("Report: UAE aid commitments to education projects increased compared to last year. #UAEAid #Education", "en", null, null),
        ("Field hospital supported by UAE aid now treating over 300 patients daily. #UAEAid #HumanitarianAid #GlobalHealth", "en", "field-clinic.jpg", "Stethoscope and medical supplies (sample stock photo)"),
        ("How is humanitarian aid coordinated across agencies? A short thread on logistics and accountability.", "en", null, null),
        ("Winter relief campaign launches with blankets, heaters and food baskets. #UAEAid #WinterRelief", "en", null, null),
        ("Grateful for the humanitarian aid that reached our village after the floods. #HumanitarianAid", "en", null, null),
        ("Policy brief on transparency in humanitarian aid reporting published today. #HumanitarianAid", "en", null, null),
        ("UAE aid flight delivers 40 tonnes of medical equipment. #UAEAid #GlobalHealth", "en", "aid-flight.jpg", "Aircraft wing above the clouds (sample stock photo)"),
        ("Mobile clinics funded through UAE aid reach remote communities. #UAEAid #HumanitarianAid #GlobalHealth", "en", "field-clinic.jpg", "Stethoscope and medical supplies (sample stock photo)"),
        ("Women-led cooperatives receive grants as part of a humanitarian aid recovery programme. #HumanitarianAid #WomenEmpowerment", "en", null, null),
        ("Reconstruction of schools begins with support from UAE aid partners. #UAEAid #Education", "en", "school-build.jpg", "Building under construction with a crane (sample stock photo)"),
        ("Data shows humanitarian aid response times improved by 30% this quarter. #HumanitarianAid #Logistics", "en", null, null),
        ("وصول قافلة مساعدات إماراتية جديدة تحمل مواد غذائية وطبية. #UAEAid #الإمارات", "ar", "convoy.jpg", "Trucks lined up on a highway (sample stock photo)"),
        ("المساعدات الإنسانية تصل إلى آلاف الأسر المتضررة هذا الأسبوع. #HumanitarianAid #الإمارات", "ar", null, null),
        ("Weekend market update: fresh produce prices stable across the city.", "en", null, null),
        ("Traffic advisory for the coastal road this evening due to maintenance works.", "en", null, null),
        ("Join our webinar on supply-chain resilience for humanitarian aid organisations. #HumanitarianAid #Logistics", "en", null, null),
    ];
}
