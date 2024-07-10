using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace AtmosphereRendering
{
    [System.Serializable]
    public class SharedProperties
    {

        const int BAYER_LIMIT = 16;

        // 4 x 4 Bayer matrix
        static readonly int[] bayerFilter = new int[BAYER_LIMIT]{
            0,  8,  2, 10,
            12,  4, 14,  6,
            3, 11,  1,  9,
            15,  7, 13,  5
        };

        private Vector2[] FrameJitters = new Vector2[16]
        {
            new Vector2(0, 2), new Vector2(0, 1), new Vector2(3, 1), new Vector2(1, 2),
            new Vector2(0, 3), new Vector2(1, 0), new Vector2(1, 3), new Vector2(1, 1),
            new Vector2(2, 0), new Vector2(2, 1), new Vector2(3, 2), new Vector2(0, 0),
            new Vector2(2, 3), new Vector2(3, 0), new Vector2(2, 2), new Vector2(3, 3)
        };

		public float frameJitterScale = 1.0f;
        public Vector2 frameJitter;
        public Vector3 cameraPosition;
        public Matrix4x4 currentVP;
        public Matrix4x4 previousVP;
        public Matrix4x4 jitterProjectionMatrix;
		public Matrix4x4 previousjitterProjectionMatrix;
		public Matrix4x4 previousProjection;
		public Matrix4x4 previousInverseRotation;
		public Matrix4x4 previousRotation;
		public Matrix4x4 projection;
		public Matrix4x4 inverseRotation;
		public Matrix4x4 rotation;

		[SerializeField] private int _subFrameNumber;
		public int subFrameNumber { get { return _subFrameNumber; } }


		[SerializeField] private int _downsample;
		public int downsample {
			get { return _downsample; }
			set {
				_downsample = value;
			}
		}

		[SerializeField] private int _subPixelSize;
		public int subPixelSize { 
			get { return _subPixelSize; }
			set {
				_subPixelSize = value;
				_subFrameNumber = 0;
			}
		}

		private bool _dimensionsChangedSinceLastFrame;
		public bool dimensionsChangedSinceLastFrame { get { return _dimensionsChangedSinceLastFrame; } }
		
		[SerializeField] private int _subFrameWidth;
		public int subFrameWidth { get { return _subFrameWidth; } }

		[SerializeField] private int _subFrameHeight;
		public int subFrameHeight { get { return _subFrameHeight; } }

		[SerializeField] private int _frameWidth;
		public int frameWidth { get { return _frameWidth; } }

		[SerializeField] private int _frameHeight;
		public int frameHeight { get { return _frameHeight; } }

        public bool jitterProjection = true;
		public bool useFixedDimensions;
		public int fixedWidth;
		public int fixedHeight;

		private int[] _frameNumbers;
        private ComputeBuffer frameBuffer;
		[SerializeField] uint _renderCount;

		public SharedProperties()
		{
			_renderCount = 0;
			downsample = 2;
			subPixelSize = 2;
		}

		public void OnStart()
		{
			for (int i = 0; i < 16; i++)
			{
				float x = HaltonSequence(i + 1, 2) * 4; // Multiply by 4 to map to 4x4 grid
				float y = HaltonSequence(i + 1, 3) * 4; // Multiply by 4 to map to 4x4 grid
				FrameJitters[i] = new Vector2(Mathf.Round(x), Mathf.Round(y));
			}
		}


        public void BeginFrame( Camera camera)
		{
			UpdateFrameDimensions( camera);

			cameraPosition = camera.transform.position;
			projection = camera.projectionMatrix;
			rotation = camera.worldToCameraMatrix;
			inverseRotation = camera.cameraToWorldMatrix;

			frameJitter = FrameJitters[_renderCount % (subPixelSize * subPixelSize)] * frameJitterScale;

			ApplyJitterToCamera(camera, frameJitter);

		}

        public void EndFrame()
		{
			previousProjection = projection;
			previousRotation = rotation;
			previousInverseRotation = inverseRotation;
			previousjitterProjectionMatrix = jitterProjectionMatrix;
			_dimensionsChangedSinceLastFrame = false;

            
			_renderCount++;
			_subFrameNumber = bayerFilter[ _renderCount % (subPixelSize * subPixelSize)];
            
            frameBuffer.Release();
		}

        public void ApplyToCompute(ComputeShader compute)
		{
			Matrix4x4 inverseProjection = projection.inverse;
			if( jitterProjection) { inverseProjection = jitterProjectionMatrix.inverse; }


			compute.SetVector("_WorldSpaceCameraPos", cameraPosition);
            compute.SetMatrix("_CameraToWorld", inverseRotation);
            compute.SetMatrix("_CameraInverseProjection", inverseProjection);


            compute.SetMatrix("_PrevVP_NoFlip", previousVP);
            compute.SetMatrix("_CurrVP_NoFlip", currentVP);
		}

        public void ApplyToMaterial( Material material)
		{
			Matrix4x4 inverseProjection = projection.inverse;
			//if( jitterProjection) { inverseProjection = jitterProjectionMatrix.inverse; }


			material.SetMatrix( "_PreviousProjection", previousProjection);
			material.SetMatrix( "_PreviousInverseProjection", previousProjection.inverse);
			material.SetMatrix( "_PreviousRotation", previousRotation);
			material.SetMatrix( "_PreviousInverseRotation", previousInverseRotation);
			material.SetMatrix( "_Projection", projection);
			material.SetMatrix( "_InverseProjection", inverseProjection);
			material.SetMatrix( "_Rotation", rotation);

			material.SetFloat( "_SubFrameNumber", subFrameNumber);
			material.SetFloat( "_SubPixelSize", subPixelSize);
			material.SetVector( "_SubFrameSize", new Vector2( _subFrameWidth, _subFrameHeight));
			material.SetVector( "_FrameSize", new Vector2( _frameWidth, _frameHeight));

            material.SetMatrix("_PrevVP_NoFlip", previousjitterProjectionMatrix * previousRotation);
            material.SetMatrix("_CurrVP_NoFlip", jitterProjectionMatrix * rotation);
		}

        public void ApplyFameSettingsToCompute( ComputeShader compute, ref int kernelID)
        {
            // Set uInt:
            frameBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);
            frameBuffer.SetData(new uint[] { _renderCount });
            compute.SetBuffer(kernelID, "FrameCounterBuffer", frameBuffer);

            // Set Int:
            compute.SetInt("frameInterval", subPixelSize * subPixelSize);
        }

        private void UpdateFrameDimensions( Camera camera)
		{
			int newFrameWidth = useFixedDimensions ? fixedWidth : camera.pixelWidth / downsample;
			int newFrameHeight = useFixedDimensions ? fixedHeight : camera.pixelHeight / downsample;

			while( (newFrameWidth % _subPixelSize) != 0) { newFrameWidth++; }
			while( (newFrameHeight % _subPixelSize) != 0) { newFrameHeight++; }

			int newSubFrameWidth = newFrameWidth / _subPixelSize;
			int newSubFrameHeight = newFrameHeight / _subPixelSize;

			_dimensionsChangedSinceLastFrame = newFrameWidth != _frameWidth ||
											   newFrameHeight != _frameHeight ||
											   newSubFrameWidth != _subFrameWidth ||
											   newSubFrameHeight != _subFrameHeight;

			_frameWidth = newFrameWidth;
			_frameHeight = newFrameHeight;
			_subFrameWidth = newSubFrameWidth;
			_subFrameHeight = newSubFrameHeight;
		}


        public Vector2 GetFrameJitter()
        {
            Debug.Log("subFrameNumber: " + subFrameNumber); 
            return FrameJitters[subFrameNumber];
        }

		private static float HaltonSequence(int index, int baseValue)
		{
			float result = 0;
			float f = 1f / baseValue;
			int i = index;
			while (i > 0)
			{
				result = result + f * (i % baseValue);
				i = i / baseValue;
				f = f / baseValue;
			}
			return result;
		}


		void ApplyJitterToCamera(Camera camera, Vector2 jitter)
		{
			Matrix4x4 projectionMatrix = camera.projectionMatrix;
			projectionMatrix.m02 += jitter.x / camera.pixelWidth;
			projectionMatrix.m12 += jitter.y / camera.pixelHeight;
			jitterProjectionMatrix = projectionMatrix;
		}
    }
}
