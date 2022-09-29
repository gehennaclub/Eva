using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eva.Threads
{
    public class Factory
    {
        private List<Action> jobs { get; set; }

        public Factory()
        {
            this.jobs = new List<Action>();
        }

        public async Task Run(Action action)
        {
            jobs.Add(action);
            await Queuer();
        }

        private void Invoker(Action action)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                action();
            });
        }

        public async Task Queuer()
        {
            foreach (Action job in jobs)
            {
                await Task.Run(() => Invoker(job));
            }

            jobs.Clear();
        }
    }
}
