using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BamBot.Automation
{
    public class CompositePageActionSequence : PageActionSequence // TODO: revisit this concept; unnecessarily complex for now
    {
        public CompositePageActionSequence(string name) : base(name) 
        {
            PreExecutionPageActionSequences = new List<PageActionSequence>();
        }

        public List<PageActionSequence> PreExecutionPageActionSequences { get; set; }
        public override PageActionSequence EnableDebug(string screenshotsDirectory = null)
        {
            foreach(PageActionSequence pageActionSequence in PreExecutionPageActionSequences)
            {
                pageActionSequence.EnableDebug(screenshotsDirectory);
            }

            return base.EnableDebug(screenshotsDirectory);
        }

        public override async Task<PageActionSequenceExecutionResult> ExecuteAsync(ReExecutionStrategy reExecutionStrategy = ReExecutionStrategy.ForErrors, bool continueOnFailure = false)
        {
            // if not all executed
            if(!AllExecuted())
            {
                //      PreExecute
                await PreExecuteAsync(reExecutionStrategy, continueOnFailure);
            }

            // if not all succeeded
            if(!AnyFailed(out List<PageActionSequence> failures))
            {
                //      switch on reExecutionStrategy
                switch (reExecutionStrategy)
                {
                    case ReExecutionStrategy.Invalid:
                    case ReExecutionStrategy.ForErrors:
                        await ReExecuteFailuresAsync();
                        break;
                    case ReExecutionStrategy.Always:
                        await ExecuteAsync(PreExecutionPageActionSequences);
                        break;
                    default:
                        break;
                }
            }
            return await base.ExecuteAsync(reExecutionStrategy, continueOnFailure);
        }

        protected async Task PreExecuteAsync(ReExecutionStrategy reExecutionStrategy = ReExecutionStrategy.ForErrors, bool continueOnFailure = false)
        {
            await ExecuteAsync(PreExecutionPageActionSequences, reExecutionStrategy, continueOnFailure);
        }

        protected async Task ExecuteAsync(List<PageActionSequence> pageActionSequences, ReExecutionStrategy reExecutionStrategy = ReExecutionStrategy.ForErrors, bool continueOnFailure = false)
        {
            foreach (PageActionSequence pageActionSequence in pageActionSequences)
            {
                await pageActionSequence.ExecuteAsync(reExecutionStrategy, continueOnFailure);
            }
        }

        protected async Task ReExecuteFailuresAsync()
        {
            if(AnyFailed(out List<PageActionSequence> failures))
            {
                await ExecuteAsync(failures);
            }
        }

        protected bool AllSucceeded(List<PageActionSequence> pageActionSequences = null)
        {
            pageActionSequences = pageActionSequences ?? PreExecutionPageActionSequences;
            return !pageActionSequences.Any(p => p.Succeeded == false);
        }

        protected bool AllExecuted(List<PageActionSequence> pageActionSequences = null)
        {
            return !pageActionSequences.Any(p => p.HasExecuted == false);
        }

        protected bool AnyFailed(out List<PageActionSequence> failures, List<PageActionSequence> pageActionSequences = null)
        {
            failures = pageActionSequences.Where(p => p.Succeeded == false).ToList();
            return failures.Any();
        }
    }
}
