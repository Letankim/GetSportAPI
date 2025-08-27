using System;
using System.Collections.Generic;

namespace GetSportAPI.Models.Generated;

public partial class Playmatepost
{
    public int PostId { get; set; }

    public int UserId { get; set; }

    public int? CourtbookingId { get; set; }

    public string Title { get; set; } = null!;

    public string? Content { get; set; }

    public int Neededplayers { get; set; }

    public string? Skilllevel { get; set; }

    public string Status { get; set; } = null!;

    public DateTime Createdat { get; set; }

    public virtual Courtbooking? Courtbooking { get; set; }

    public virtual ICollection<Playmatejoin> Playmatejoins { get; set; } = new List<Playmatejoin>();

    public virtual Account User { get; set; } = null!;
}
