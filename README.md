# ALPCLogger
ALPCLogger fork with changes so dirty that I don't have the energy to clean up properly, so I'm keeping them in a remote branch

# New functionality
- Display module name for stackframe
- (BETA) Stackframe searcher
	- Currently has some bugs to be churned out, some optimizations can be made to the user experience, since the heuristic which allows this function is by default very expensive, and is not desinged to be ran across multiple threads (we tried.) Curerntly it runs constantly (building stackframe info), building 15 of them every 200ms if possible