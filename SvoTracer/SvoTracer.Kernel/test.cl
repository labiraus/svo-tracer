kernel void test(global int *out) {
  int i = get_global_id(0);
  if (get_default_queue() != 0) {
    out[i] = 1;
  } else {
    out[i] = 2;
  }
}
