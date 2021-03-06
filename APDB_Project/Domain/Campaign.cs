﻿using System;
using System.Collections;
using System.Collections.Generic;

namespace APDB_Project.Domain
{
    public class Campaign
    {
        public int IdCampaign { get; set; }
        public int? IdClient { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double PricePerSquareMeter { get; set; }
        public int?  FromIdBuilding { get; set; }
        public int?  ToIdBuilding { get; set; }
        public virtual Building FromBuilding { get; set; } 
        public virtual Building ToBuilding { get; set; } 
        public virtual Client Client { get; set; }
        public virtual List<Banner> Banners { get; set; }
        
    }
}