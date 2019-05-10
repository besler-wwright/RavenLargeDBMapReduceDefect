using System;
using System.Collections.Generic;
using System.Linq;
using Raven.Client.Documents.Indexes;

namespace RavenDBTestApril2019
{
    public class ChargeDataSearchIndex : AbstractIndexCreationTask<Charge, ChargeDataSearchIndex.Result>
    {
        public ChargeDataSearchIndex()
        {
            Map = detailData => from dd in detailData
                select new Result
                {
                    Id = dd.Id,
                    PatientId = dd.PatientId,
                    GLAccount = dd.GLAccount,
                    GLDepartment = dd.GLAccount,
                    PostedDate = dd.PostedDate,
                    DischargeDate = dd.DischargeDate,
                    Amount = dd.Amount,
                    TagIds = dd.Tags.Select(x => x.Id).ToList(),
                    Combined_Tags = dd.Tags == null ? "" : string.Join("|", dd.Tags.OrderBy(x => x.Id).Select(x => x.Id)),


                    ContentsForSearch = new object[]
                    {
                        dd.PatientId,
                        dd.GLAccount,
                        dd.GLAccount,
                        dd.Amount,
                        $"GeneralLedgerDepartment{dd.GLDepartment}",
                        $"GeneralLedgerAccount{dd.GLAccount}"
                    },

                };
        }

        public class Result
        {
            public string Id { get; set; }
            public string PatientId { get; set; }
            public string GLAccount { get; set; }
            public string GLDepartment { get; set; }
            public DateTime? PostedDate { get; set; }
            public DateTime? DischargeDate { get; set; }
            public decimal Amount { get; set; }
            public List<string> TagIds { get; set; }
            public string Combined_Tags { get; set; }
            public object[] ContentsForSearch { get; set; }
            
        }
    }
}