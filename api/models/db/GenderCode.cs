﻿using System;
using System.Collections.Generic;

namespace SS.Api.Models.DB
{
    public partial class GenderCode
    {
        public GenderCode()
        {
            Sheriff = new HashSet<Sheriff>();
        }

        public string GenderCode1 { get; set; }
        public string Description { get; set; }
        public DateTime EffectiveDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string CreatedBy { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime CreatedDtm { get; set; }
        public DateTime UpdatedDtm { get; set; }
        public decimal RevisionCount { get; set; }

        public virtual ICollection<Sheriff> Sheriff { get; set; }
    }
}