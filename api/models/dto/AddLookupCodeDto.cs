﻿using System;
using SS.Db.models.lookupcodes;

namespace SS.Api.models.dto
{
    public class AddLookupCodeDto
    {
        public LookupTypes Type { get; set; }
        public string Code { get; set; }
        public string Description { get; set; }
        public DateTimeOffset? EffectiveDate { get; set; }
        public DateTimeOffset? ExpiryDate { get; set; }
        public int? LocationId { get; set; }
        public AddLookupSortOrderDto SortOrderForLocation { get; set; }
    }
}
