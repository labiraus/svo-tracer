

__kernel void sine_wave(__write_only image2d_t outputImage, unsigned int width,
                        unsigned int height, float time) {
  unsigned int x = get_global_id(0);
  unsigned int y = get_global_id(1);
  int2 coord = (int2)(x, y);
  // calculate uv coordinates
  float u = x / (float)width;
  float v = y / (float)height;
  u = u * 2.0f - 1.0f;
  v = v * 2.0f - 1.0f;

  // calculate simple sine wave pattern
  float freq = 4.0f;
  float w = sin(u * freq + time) * cos(v * freq + time) * 0.5f;
  write_imagef(outputImage, coord, (float4)(1, w, 0, 1));
}