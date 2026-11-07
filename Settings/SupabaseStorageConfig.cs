namespace SportsCenterAPI.Settings;

public class SupabaseStorageConfig
{
    public string Url { get; set; } = string.Empty;        // https://<project>.supabase.co
    public string ServiceKey { get; set; } = string.Empty; // service_role key (server only)
    public string Bucket { get; set; } = "images";         // phải là public bucket
}
