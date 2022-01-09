uint increment2(local uint localQueue[4], global uint *queueID, bool action) {
  int localQueuePosition = get_local_id(0) % 4;
  // Reset localQueue to 0;
  if (get_local_id(0) < 4 && get_local_id(1) == 0)
    localQueue[localQueuePosition] = 0;
  work_group_barrier(CLK_LOCAL_MEM_FENCE);
  uint id = 0;
  // Increment localQueue if an action is being performed
  if (action)
    id = atomic_inc(&localQueue[localQueuePosition]);

  work_group_barrier(CLK_LOCAL_MEM_FENCE);
  if (get_local_id(0) == 0 && get_local_id(1) == 0) {
    uint sum = localQueue[0] + localQueue[1] + localQueue[2] + localQueue[3];
    // Only one local thread updates the global variable
    // If no local thread is performing an action, no further work is required
    if (sum > 0)
      localQueue[4] = atomic_add(queueID, sum);
  }

  // Final id is global variable's starting value, plus the local queue position, plus total for previous local queues
  if (localQueuePosition > 0)
    id += localQueue[0];
  if (localQueuePosition > 1)
    id += localQueue[1];
  if (localQueuePosition > 2)
    id += localQueue[2];
  work_group_barrier(CLK_LOCAL_MEM_FENCE);
  return id + localQueue[4];
}

uint increment(local uint localQueue[5], global uint *queueID, bool action) {
  int localQueuePosition = get_local_id(0) % 4;
  // Reset localQueue to 0;
  if (get_local_id(0) < 5 && get_local_id(1) == 0)
    localQueue[get_local_id(0)] = 0;
  work_group_barrier(CLK_LOCAL_MEM_FENCE);

  uint id = 0;
  // Increment localQueue if an action is being performed
  if (action)
    id = atomic_inc(&localQueue[localQueuePosition]);

  work_group_barrier(CLK_LOCAL_MEM_FENCE);

  atomic_work_item_fence(CLK_LOCAL_MEM_FENCE, memory_order_acquire, memory_scope_work_item);
  if (get_local_id(0) == 0 && get_local_id(1) == 0) {
    uint sum = localQueue[0] + localQueue[1] + localQueue[2] + localQueue[3];
    // Only one local thread updates the global variable
    // If no local thread is performing an action, no further work is required
    if (sum > 0)
      localQueue[4] = atomic_add(queueID, sum);
    atomic_work_item_fence(CLK_LOCAL_MEM_FENCE, memory_order_release, memory_scope_work_group);
  }

  // Final id is global variable's starting value, plus the local queue position, plus total for previous local queues
  if (localQueuePosition > 0)
    id += localQueue[0];
  if (localQueuePosition > 1)
    id += localQueue[1];
  if (localQueuePosition > 2)
    id += localQueue[2];
  // Ensure atomic_add takes place before final return
  atomic_work_item_fence(CLK_LOCAL_MEM_FENCE, memory_order_acq_rel, memory_scope_work_item);
  // work_group_barrier(CLK_LOCAL_MEM_FENCE);
  if (action)
    return id + localQueue[4];
  else
    return 0;
}

kernel void test1(global uint *queueID, global uint *idQueue) {
  uint id = atomic_add(queueID, 1);
  idQueue[id] = 1;
}

kernel void test2(global uint *queueID, global uint *idQueue) {
  local uint localQueue[5];
  uint id = increment2(localQueue, queueID, true);
  idQueue[id] = 1;
}
kernel void test3(global uint *queueID, global uint *idQueue) {
  local uint localQueue[5];
  uint id = increment(localQueue, queueID, true);
  idQueue[id] = 1;
}

kernel void AtomicSum1(global int *sum) { atomic_add(sum, 1); }

kernel void AtomicSum2(global int *sum) {
  local int tmpSum[1];
  if (get_local_id(0) == 0) {
    tmpSum[0] = 0;
  }
  barrier(CLK_LOCAL_MEM_FENCE);
  atomic_add(&tmpSum[0], 1);
  barrier(CLK_LOCAL_MEM_FENCE);
  if (get_local_id(0) == (get_local_size(0) - 1)) {
    atomic_add(sum, tmpSum[0]);
  }
}

kernel void AtomicSum3(global int *sum) {
  local int tmpSum[4];
  if (get_local_id(0) < 4) {
    tmpSum[get_local_id(0)] = 0;
  }
  barrier(CLK_LOCAL_MEM_FENCE);
  atomic_add(&tmpSum[get_global_id(0) % 4], 1);
  barrier(CLK_LOCAL_MEM_FENCE);
  if (get_local_id(0) == (get_local_size(0) - 1)) {
    atomic_add(sum, tmpSum[0] + tmpSum[1] + tmpSum[2] + tmpSum[3]);
  }
}
