using System;
using System.Collections.Generic;

namespace GetSportAPI.Models.Generated;

public partial class Playmatejoin
{
    public int JoinId { get; set; }

    public int PostId { get; set; }

    public int UserId { get; set; }

    public DateTime Joinedat { get; set; }

    public virtual Playmatepost Post { get; set; } = null!;

    public virtual Account User { get; set; } = null!;
}
