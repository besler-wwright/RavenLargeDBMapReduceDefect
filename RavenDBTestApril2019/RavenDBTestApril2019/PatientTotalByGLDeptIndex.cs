using System.Linq;
using Raven.Client.Documents.Indexes;

namespace RavenDBTestApril2019
{
    public class PatientTotalByGLDeptIndex : AbstractMultiMapIndexCreationTask<PatientTotalByGLDeptIndex.Result>
    {

        public class Result
        {
            public string PatientId { get; set; }
            public string GLDepartment { get; set; }
            public int TransactionCount { get; set; }
            public decimal Amount { get; set; }
        }

        public PatientTotalByGLDeptIndex()
        {
            AddMap<Payment>(payments =>
                from payment in payments
                select new Result()
                {
                    PatientId = payment.PatientId,
                    GLDepartment = payment.GLDepartment,
                    Amount = payment.Amount,
                    TransactionCount = 1
                });

            AddMap<Charge>(charges =>
                from charge in charges
                select new Result()
                {
                    PatientId = charge.PatientId,
                    GLDepartment = charge.GLDepartment,
                    Amount = charge.Amount,
                    TransactionCount = 1
                });

            Reduce = results => from result in results
                group result by new { result.PatientId, result.GLDepartment } into g
                select new
                {
                    g.Key.PatientId,
                    g.Key.GLDepartment,
                    TransactionCount = g.Sum(z => z.TransactionCount),
                    Amount = g.Sum(z => z.Amount),
                };
        }
    }
}