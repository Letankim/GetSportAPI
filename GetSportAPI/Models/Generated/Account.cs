using System;
using System.Collections.Generic;

namespace GetSportAPI.Models.Generated;

public partial class Account
{
    public int UserId { get; set; }

    public string Role { get; set; } = null!;

    public string Fullname { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string? Gender { get; set; }

    public string? Phonenumber { get; set; }

    public string? Email { get; set; }

    public DateOnly? Dateofbirth { get; set; }

    public string? Skilllevel { get; set; }

    public string? Membershiptype { get; set; }

    public int Totalpoint { get; set; }

    public DateTime Createat { get; set; }

    public string? Qrcode { get; set; }

    public bool Isactive { get; set; }

    public string? Status { get; set; }

    public virtual ICollection<Blogpost> Blogposts { get; set; } = new List<Blogpost>();

    public virtual ICollection<Courtbooking> Courtbookings { get; set; } = new List<Courtbooking>();

    public virtual ICollection<Court> Courts { get; set; } = new List<Court>();

    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

    public virtual ICollection<Ownerpackage> Ownerpackages { get; set; } = new List<Ownerpackage>();

    public virtual ICollection<Playmatejoin> Playmatejoins { get; set; } = new List<Playmatejoin>();

    public virtual ICollection<Playmatepost> Playmateposts { get; set; } = new List<Playmatepost>();

    public virtual ICollection<Pointtransaction> Pointtransactions { get; set; } = new List<Pointtransaction>();

    public virtual ICollection<Userpackage> Userpackages { get; set; } = new List<Userpackage>();

    public virtual ICollection<Uservoucher> Uservouchers { get; set; } = new List<Uservoucher>();

    public virtual Wallet? Wallet { get; set; }
}
