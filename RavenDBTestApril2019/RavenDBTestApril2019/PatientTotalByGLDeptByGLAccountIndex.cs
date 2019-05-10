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

    public class PatientTotalByGLAccountIndex : AbstractMultiMapIndexCreationTask<PatientTotalByGLAccountIndex.Result>
    {

        public class Result
        {
            public string PatientId { get; set; }
            public string GLAccount { get; set; }
            public int TransactionCount { get; set; }
            public decimal Amount { get; set; }
        }

        public PatientTotalByGLAccountIndex()
        {
            AddMap<Payment>(payments =>
                from payment in payments
                select new Result()
                {
                    PatientId = payment.PatientId,
                    GLAccount = payment.GLAccount,
                    Amount = payment.Amount,
                    TransactionCount = 1
                });

            AddMap<Charge>(charges =>
                from charge in charges
                select new Result()
                {
                    PatientId = charge.PatientId,
                    GLAccount = charge.GLAccount,
                    Amount = charge.Amount,
                    TransactionCount = 1
                });

            Reduce = results => from result in results
                group result by new { result.PatientId, result.GLAccount } into g
                select new
                {
                    g.Key.PatientId,
                    g.Key.GLAccount,
                    TransactionCount = g.Sum(z => z.TransactionCount),
                    Amount = g.Sum(z => z.Amount),
                };
        }
    }

}