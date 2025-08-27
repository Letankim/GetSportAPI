using System;
using System.Collections.Generic;

namespace GetSportAPI.Models.Generated;

public partial class Ownerpackage
{
    public int OwnerpackageId { get; set; }

    public int OwnerId { get; set; }

    public string Packagename { get; set; } = null!;

    public int Duration { get; set; }

    public DateOnly Startdate { get; set; }

    public DateOnly Enddate { get; set; }

    public decimal Price { get; set; }

    public string Status { get; set; } = null!;

    public DateTime Createat { get; set; }

    public int Priority { get; set; }

    public virtual Account Owner { get; set; } = null!;
}
