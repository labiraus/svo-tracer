kernel void test(global int *a, global int *b, global float *c) {
  queue_t default_queue = get_default_queue();
  int i = get_global_id(0);
  if (default_queue != 0) {
    c[i] = 1;
    // enqueue_kernel(default_queue, CLK_ENQUEUE_FLAGS_WAIT_KERNEL, ndrange_1D(1),
    //                ^(void){
    //                });
  } else {
    c[i] = 2;
  }
}
