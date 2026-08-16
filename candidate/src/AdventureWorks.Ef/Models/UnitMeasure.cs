using System;
using System.Collections.Generic;

namespace AdventureWorks.Ef.Models;

/// <summary>
/// Unit of measure lookup table.
/// </summary>
public partial class UnitMeasure
{
    /// <summary>
    /// Primary key.
    /// </summary>
    public string UnitMeasureCode { get; set; } = null!;

    /// <summary>
    /// Unit of measure description.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Date and time the record was last updated.
    /// </summary>
    public DateTime ModifiedDate { get; set; }

    public virtual ICollection<BillOfMaterials> BillOfMaterials { get; set; } = new List<BillOfMaterials>();

    public virtual ICollection<Product> ProductSizeUnitMeasureCodeNavigation { get; set; } = new List<Product>();

    public virtual ICollection<Product> ProductWeightUnitMeasureCodeNavigation { get; set; } = new List<Product>();
}
