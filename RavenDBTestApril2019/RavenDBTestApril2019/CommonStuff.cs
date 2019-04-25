using System;
using System.Collections.Generic;

namespace RavenDBTestApril2019
{
    public abstract class CommonStuff
    {
        protected CommonStuff()
        {
            Tags = new List<ITagDocument>();
        }

        public string Id { get; set; }
        public string PatientId { get; set; }
        public string GLAccount { get; set; }
        public string GLDepartment { get; set; }
        public DateTime? PostedDate { get; set; }
        public DateTime? DischargeDate { get; set; }
        public decimal Amount { get; set; }

        public List<ITagDocument> Tags { get; set; }
    }
}