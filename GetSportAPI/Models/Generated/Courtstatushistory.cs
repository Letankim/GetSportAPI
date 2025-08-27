using System;
using System.Collections.Generic;

namespace GetSportAPI.Models.Generated;

public partial class Courtstatushistory
{
    public int StatusId { get; set; }

    public int CourtId { get; set; }

    public string Statusofcourt { get; set; } = null!;

    public DateTime Updateat { get; set; }

    public virtual Court Court { get; set; } = null!;
}
