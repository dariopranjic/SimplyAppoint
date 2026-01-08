using SimplyAppoint.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimplyAppoint.DataAccess.Repository.IRepository
{
    public interface ITimeOffRepository : IRepository<TimeOff>
    {
        void Update(TimeOff obj);
    }
}
