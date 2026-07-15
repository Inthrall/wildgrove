namespace Wildgrove.Data
{
    /// <summary>
    /// Authoring entry for a raw gatherable resource's Provisioner sell value
    /// (design/data/resources.json). Crafted trade goods derive their value from
    /// their recipe; only gathered resources are priced here.
    /// </summary>
    public sealed class ResourceDef
    {
        public string Id { get; set; }
        public double SellValue { get; set; }
    }
}
