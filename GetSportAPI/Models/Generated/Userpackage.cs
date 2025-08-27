using System;
using System.Collections.Generic;

namespace GetSportAPI.Models.Generated;

public partial class Userpackage
{
    public int UserpackageId { get; set; }

    public int UserId { get; set; }

    public int PackageId { get; set; }

    public DateOnly Startdate { get; set; }

    public DateOnly Enddate { get; set; }

    public bool Isactive { get; set; }

    public DateTime Createat { get; set; }

    public DateTime? Updateat { get; set; }

    public virtual Package Package { get; set; } = null!;

    public virtual Account User { get; set; } = null!;
}
