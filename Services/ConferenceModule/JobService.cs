using TASA.Models;
using TASA.Program;

namespace TASA.Services.ConferenceModule
{
    public class JobService(ServiceWrapper service) : IService
    {
        public void DoEcs(Conference conference)
        {
            if (conference.Ecs.Count > 0)
            {
                conference.Ecs.ToList().ForEach(x => service.EcsService.Send(x.Id));
            }
        }
    }
}
