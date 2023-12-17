# ALPCLogger
ALPCLogger fork with changes so dirty that I don't have the energy to clean up properly, so I'm keeping them in a remote branch

# New functionality
- Display module name for stackframe
- (BETA) Stackframe searcher
	- Currently has some bugs to be churned out
   	- May be very slow, and that's to be expected. On my Ryzen 9 5900 it takes ~1.9s to generate 30 debug stacktraces
	- To make this usable, we have "Focused" filtering, which prioritizes building stacktraces for what matches the search text criteria.
	- It builds 30 stacktraces per go if there are enough events matching filter criteria for that. It doesn't rebuild existing traces, and it skips existing traces. If the iteration goes through an existing trace, it isn't counted, it will still build as many as possible.
