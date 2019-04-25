using System;
using System.Collections.Generic;
using System.Linq;
using Raven.Client.Documents.Indexes;

namespace RavenDBTestApril2019
{
    public class PatientTotalByGLDeptByGLAccountIndex : AbstractMultiMapIndexCreationTask<PatientTotalByGLDeptByGLAccountIndex.Result>
    {

        public class Result
        {
            public string PatientId { get; set; }
            public string GLAccount { get; set; }
            public string GLDepartment { get; set; }
            public int TransactionCount { get; set; }
            public decimal Amount { get; set; }
        }

        public PatientTotalByGLDeptByGLAccountIndex()
        {
            AddMap<Payment>(payments =>
                from payment in payments
                select new Result()
                {
                    PatientId = payment.PatientId,
                    GLAccount = payment.GLAccount,
                    GLDepartment = payment.GLDepartment,
                    Amount = payment.Amount,
                    TransactionCount = 1
                });

            AddMap<Charge>(charges =>
                from charge in charges
                select new Result()
                {
                    PatientId = charge.PatientId,
                    GLAccount = charge.GLAccount,
                    GLDepartment = charge.GLDepartment,
                    Amount = charge.Amount,
                    TransactionCount = 1
                });

            Reduce = results => from result in results
                group result by new { result.PatientId, result.GLDepartment, result.GLAccount} into g
                select new
                {
                    g.Key.PatientId,
                    g.Key.GLDepartment,
                    g.Key.GLAccount,
                    TransactionCount = g.Sum(z => z.TransactionCount),
                    Amount = g.Sum(z => z.Amount),
                };
        }
    }

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