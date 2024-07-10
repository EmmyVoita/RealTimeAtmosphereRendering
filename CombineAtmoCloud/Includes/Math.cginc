


float2 squareUV(float2 uv, float2 _ScreenParams) 
{
    float width = _ScreenParams.x;
    float height =_ScreenParams.y;
    //float minDim = min(width, height);
    float scale = 1000;
    float x = uv.x * width;
    float y = uv.y * height;
    return float2 (x/scale, y/scale);
}




