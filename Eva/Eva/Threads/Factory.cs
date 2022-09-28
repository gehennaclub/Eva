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
        public async Task Run(Action action)
        {
            await Task.Run(() => Invoker(action));
        }

        private void Invoker(Action action)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                action();
            });
        }
    }
}
