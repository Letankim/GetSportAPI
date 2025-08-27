using System;
using System.Collections.Generic;

namespace GetSportAPI.Models.Generated;

public partial class Blogpost
{
    public int BlogId { get; set; }

    public int AccountId { get; set; }

    public string Title { get; set; } = null!;

    public string? Shortdesc { get; set; }

    public string? Content { get; set; }

    public DateOnly? Dateofbirth { get; set; }

    public string? Imageurl { get; set; }

    public DateTime Createdat { get; set; }

    public DateTime? Updatedat { get; set; }

    public string? Status { get; set; }

    public virtual Account Account { get; set; } = null!;
}
