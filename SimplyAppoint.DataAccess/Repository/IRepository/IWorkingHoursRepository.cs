using SimplyAppoint.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimplyAppoint.DataAccess.Repository.IRepository
{
    public interface IWorkingHoursRepository : IRepository<WorkingHours>
    {
        void Update(WorkingHours obj);
    }
}
