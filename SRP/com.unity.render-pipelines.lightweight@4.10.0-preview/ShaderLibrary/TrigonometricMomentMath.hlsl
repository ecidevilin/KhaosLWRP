#ifndef LIGHTWEIGHT_TRIGONOMETRIC_MOMENT_MATH_INCLUDED
#define LIGHTWEIGHT_TRIGONOMETRIC_MOMENT_MATH_INCLUDED

/*! Returns the complex conjugate of the given complex number (i.e. it changes
	the sign of the y-component).*/
float2 Conjugate(float2 Z) {
	return float2(Z.x, -Z.y);
}
/*! This function implements complex multiplication.*/
float2 Multiply(float2 LHS, float2 RHS) {
	return float2(LHS.x*RHS.x - LHS.y*RHS.y, LHS.x*RHS.y + LHS.y*RHS.x);
}
/*! This function computes the magnitude of the given complex number.*/
float Magnitude(float2 Z) {
	return sqrt(dot(Z, Z));
}
/*! This function computes the quotient of two complex numbers. The denominator
	must not be zero.*/
float2 Divide(float2 Numerator, float2 Denominator) {
	return float2(Numerator.x*Denominator.x + Numerator.y*Denominator.y, -Numerator.x*Denominator.y + Numerator.y*Denominator.x) / dot(Denominator, Denominator);
}
/*! This function divides a real number by a complex number. The denominator
	must not be zero.*/
float2 Divide(float Numerator, float2 Denominator) {
	return float2(Numerator*Denominator.x, -Numerator * Denominator.y) / dot(Denominator, Denominator);
}
/*! This function implements computation of the reciprocal of the given non-
	zero complex number.*/
float2 Reciprocal(float2 Z) {
	return float2(Z.x, -Z.y) / dot(Z, Z);
}
/*! This utility function implements complex squaring.*/
float2 Square(float2 Z) {
	return float2(Z.x*Z.x - Z.y*Z.y, 2.0f*Z.x*Z.y);
}
/*! This utility function implements complex computation of the third power.*/
float2 Cube(float2 Z) {
	return Multiply(Square(Z), Z);
}
/*! This utility function computes one square root of the given complex value.
	The other one can be found using the unary minus operator.
  \warning This function is continuous but not defined on the negative real
			axis (and cannot be continued continuously there).
  \sa SquareRoot() */
float2 SquareRootUnsafe(float2 Z) {
	float ZLengthSq = dot(Z, Z);
	float ZLengthInv = rsqrt(ZLengthSq);
	float2 UnnormalizedRoot = Z * ZLengthInv + float2(1.0f, 0.0f);
	float UnnormalizedRootLengthSq = dot(UnnormalizedRoot, UnnormalizedRoot);
	float NormalizationFactorInvSq = UnnormalizedRootLengthSq * ZLengthInv;
	float NormalizationFactor = rsqrt(NormalizationFactorInvSq);
	return NormalizationFactor * UnnormalizedRoot;
}
/*! This utility function computes one square root of the given complex value.
	The other one can be found using the unary minus operator.
  \note This function has discontinuities for values with real part zero.
  \sa SquareRootUnsafe() */
float2 SquareRoot(float2 Z) {
	float2 ZPositiveRealPart = float2(abs(Z.x), Z.y);
	float2 ComputedRoot = SquareRootUnsafe(ZPositiveRealPart);
	return (Z.x >= 0.0) ? ComputedRoot : ComputedRoot.yx;
}
/*! This utility function computes one cubic root of the given complex value. The
   other roots can be found by multiplication by cubic roots of unity.
  \note This function has various discontinuities.*/
float2 CubicRoot(float2 Z) {
	float Argument = atan2(Z.y, Z.x);
	float NewArgument = Argument / 3.0f;
	float2 NormalizedRoot;
	sincos(NewArgument, NormalizedRoot.y, NormalizedRoot.x);
	return NormalizedRoot * pow(dot(Z, Z), 1.0f / 6.0f);
}

/*! @{
   Returns the complex conjugate of the given complex vector (i.e. it changes the
   second column resp the y-component).*/
float2x2 Conjugate(float2x2 Vector) {
	return float2x2(Vector[0].x, -Vector[0].y, Vector[1].x, -Vector[1].y);
}
float3x2 Conjugate(float3x2 Vector) {
	return float3x2(Vector[0].x, -Vector[0].y, Vector[1].x, -Vector[1].y, Vector[2].x, -Vector[2].y);
}
float4x2 Conjugate(float4x2 Vector) {
	return float4x2(Vector[0].x, -Vector[0].y, Vector[1].x, -Vector[1].y, Vector[2].x, -Vector[2].y, Vector[3].x, -Vector[3].y);
}
void Conjugate(out float2 OutConjugateVector[5], float2 Vector[5]) {
	[unroll] for (int i = 0; i != 5; ++i) {
		OutConjugateVector[i] = float2(Vector[i].x, -Vector[i].x);
	}
}
//!@}

/*! Returns the real part of a complex number as real.*/
float RealPart(float2 Z) {
	return Z.x;
}

/*! Given coefficients of a quadratic polynomial A*x^2+B*x+C, this function
	outputs its two complex roots.*/
void SolveQuadratic(out float2 pOutRoot[2], float2 A, float2 B, float2 C)
{
	// Normalize the coefficients
	float2 InvA = Reciprocal(A);
	B = Multiply(B, InvA);
	C = Multiply(C, InvA);
	// Divide the middle coefficient by two
	B *= 0.5f;
	// Apply the quadratic formula
	float2 DiscriminantRoot = SquareRoot(Square(B) - C);
	pOutRoot[0] = -B - DiscriminantRoot;
	pOutRoot[1] = -B + DiscriminantRoot;
}

/*! Given coefficients of a cubic polynomial A*x^3+B*x^2+C*x+D, this function
	outputs its three complex roots.*/
void SolveCubicBlinn(out float2 pOutRoot[3], float2 A, float2 B, float2 C, float2 D)
{
	// Normalize the polynomial
	float2 InvA = Reciprocal(A);
	B = Multiply(B, InvA);
	C = Multiply(C, InvA);
	D = Multiply(D, InvA);
	// Divide middle coefficients by three
	B /= 3.0f;
	C /= 3.0f;
	// Compute the Hessian and the discriminant
	float2 Delta00 = -Square(B) + C;
	float2 Delta01 = -Multiply(C, B) + D;
	float2 Delta11 = Multiply(B, D) - Square(C);
	float2 Discriminant = 4.0f*Multiply(Delta00, Delta11) - Square(Delta01);
	// Compute coefficients of the depressed cubic 
	// (third is zero, fourth is one)
	float2 DepressedD = -2.0f*Multiply(B, Delta00) + Delta01;
	float2 DepressedC = Delta00;
	// Take the cubic root of a complex number avoiding cancellation
	float2 DiscriminantRoot = SquareRoot(-Discriminant);
	DiscriminantRoot = faceforward(DiscriminantRoot, DiscriminantRoot, DepressedD);
	float2 CubedRoot = DiscriminantRoot - DepressedD;
	float2 FirstRoot = CubicRoot(0.5f*CubedRoot);
	float2 pCubicRoot[3] = {
		FirstRoot,
		Multiply(float2(-0.5f,-0.5f*sqrt(3.0f)),FirstRoot),
		Multiply(float2(-0.5f, 0.5f*sqrt(3.0f)),FirstRoot)
	};
	// Also compute the reciprocal cubic roots
	float2 InvFirstRoot = Reciprocal(FirstRoot);
	float2 pInvCubicRoot[3] = {
		InvFirstRoot,
		Multiply(float2(-0.5f, 0.5f*sqrt(3.0f)),InvFirstRoot),
		Multiply(float2(-0.5f,-0.5f*sqrt(3.0f)),InvFirstRoot)
	};
	// Turn them into roots of the depressed cubic and revert the depression 
	// transform
	[unroll]
	for (int i = 0; i != 3; ++i)
	{
		pOutRoot[i] = pCubicRoot[i] - Multiply(DepressedC, pInvCubicRoot[i]) - B;
	}
}


/*! Given coefficients of a quartic polynomial A*x^4+B*x^3+C*x^2+D*x+E, this
	function outputs its four complex roots.*/
void SolveQuarticNeumark(out float2 pOutRoot[4], float2 A, float2 B, float2 C, float2 D, float2 E)
{
	// Normalize the polynomial
	float2 InvA = Reciprocal(A);
	B = Multiply(B, InvA);
	C = Multiply(C, InvA);
	D = Multiply(D, InvA);
	E = Multiply(E, InvA);
	// Construct a normalized cubic
	float2 P = -2.0f*C;
	float2 Q = Square(C) + Multiply(B, D) - 4.0f*E;
	float2 R = Square(D) + Multiply(Square(B), E) - Multiply(Multiply(B, C), D);
	// Compute a root that is not the smallest of the cubic
	float2 pCubicRoot[3];
	SolveCubicBlinn(pCubicRoot, float2(1.0f, 0.0f), P, Q, R);
	float2 y = (dot(pCubicRoot[1], pCubicRoot[1]) > dot(pCubicRoot[0], pCubicRoot[0])) ? pCubicRoot[1] : pCubicRoot[0];

	// Solve a quadratic to obtain linear coefficients for quadratic polynomials
	float2 BB = Square(B);
	float2 fy = 4.0f*y;
	float2 BB_fy = BB - fy;
	float2 tmp = SquareRoot(BB_fy);
	float2 G = (B + tmp)*0.5f;
	float2 g = (B - tmp)*0.5f;
	// Construct the corresponding constant coefficients
	float2 Z = C - y;
	tmp = Divide(0.5f*Multiply(B, Z) - D, tmp);
	float2 H = Z * 0.5f + tmp;
	float2 h = Z * 0.5f - tmp;

	// Compute the roots
	float2 pQuadraticRoot[2];
	SolveQuadratic(pQuadraticRoot, float2(1.0f, 0.0f), G, H);
	pOutRoot[0] = pQuadraticRoot[0];
	pOutRoot[1] = pQuadraticRoot[1];
	SolveQuadratic(pQuadraticRoot, float2(1.0f, 0.0f), g, h);
	pOutRoot[2] = pQuadraticRoot[0];
	pOutRoot[3] = pQuadraticRoot[1];
}

/*! This utility function turns a point on the unit circle into a scalar
	parameter. It is guaranteed to grow monotonically for (cos(phi),sin(phi))
	with phi in 0 to 2*pi. There are no other guarantees. In particular it is
	not an arclength parametrization. If you change this function, you must
	also change circleToParameter() in MomentOIT.cpp.*/
float circleToParameter(float2 circle_point) {
	float result = abs(circle_point.y) - abs(circle_point.x);
	result = (circle_point.x < 0.0f) ? (2.0f - result) : result;
	return (circle_point.y < 0.0f) ? (6.0f - result) : result;
}

/*! This utility function returns the appropriate weight factor for a root at
	the given location. Both inputs are supposed to be unit vectors. If a
	circular arc going counter clockwise from (1.0,0.0) meets root first, it
	returns 1.0, otherwise 0.0 or a linear ramp in the wrapping zone.*/
float getRootWeightFactor(float reference_parameter, float root_parameter, float4 wrapping_zone_parameters) {
	float binary_weight_factor = (root_parameter < reference_parameter) ? 1.0f : 0.0f;
	float linear_weight_factor = saturate(mad(root_parameter, wrapping_zone_parameters.z, wrapping_zone_parameters.w));
	return binary_weight_factor + linear_weight_factor;
}

/*! This function reconstructs the transmittance at the given depth from two
	normalized trigonometric moments.*/
float ComputeTransmittanceTrigonometric(float b_0, float2 trig_b[2], float depth, float bias, float overestimation, float4 wrapping_zone_parameters)
{
	// Apply biasing and reformat the inputs a little bit
	float moment_scale = 1.0f - bias;
	float2 b[3] = {
		float2(1.0f, 0.0f),
		trig_b[0] * moment_scale,
		trig_b[1] * moment_scale
	};
	// Compute a Cholesky factorization of the Toeplitz matrix
	float D00 = RealPart(b[0]);
	float InvD00 = 1.0f / D00;
	float2 L10 = (b[1])*InvD00;
	float D11 = RealPart(b[0] - D00 * Multiply(L10, Conjugate(L10)));
	float InvD11 = 1.0f / D11;
	float2 L20 = (b[2])*InvD00;
	float2 L21 = (b[1] - D00 * Multiply(L20, Conjugate(L10)))*InvD11;
	float D22 = RealPart(b[0] - D00 * Multiply(L20, Conjugate(L20)) - D11 * Multiply(L21, Conjugate(L21)));
	float InvD22 = 1.0f / D22;
	// Solve a linear system to get the relevant polynomial
	float phase = mad(depth, wrapping_zone_parameters.y, wrapping_zone_parameters.y);
	float2 circle_point;
	sincos(phase, circle_point.y, circle_point.x);
	float2 c[3] = {
		float2(1.0f,0.0f),
		circle_point,
		Multiply(circle_point, circle_point)
	};
	c[1] -= Multiply(L10, c[0]);
	c[2] -= Multiply(L20, c[0]) + Multiply(L21, c[1]);
	c[0] *= InvD00;
	c[1] *= InvD11;
	c[2] *= InvD22;
	c[1] -= Multiply(Conjugate(L21), c[2]);
	c[0] -= Multiply(Conjugate(L10), c[1]) + Multiply(Conjugate(L20), c[2]);
	// Compute roots of the polynomial
	float2 pRoot[2];
	SolveQuadratic(pRoot, Conjugate(c[2]), Conjugate(c[1]), Conjugate(c[0]));
	// Figure out how to weight the weights
	float depth_parameter = circleToParameter(circle_point);
	float3 weight_factor;
	weight_factor[0] = overestimation;
	[unroll]
	for (int i = 0; i != 2; ++i)
	{
		float root_parameter = circleToParameter(pRoot[i]);
		weight_factor[i + 1] = getRootWeightFactor(depth_parameter, root_parameter, wrapping_zone_parameters);
	}
	// Compute the appropriate linear combination of weights
	float2 z[3] = { circle_point, pRoot[0], pRoot[1] };
	float f0 = weight_factor[0];
	float f1 = weight_factor[1];
	float f2 = weight_factor[2];
	float2 f01 = Divide(f1 - f0, z[1] - z[0]);
	float2 f12 = Divide(f2 - f1, z[2] - z[1]);
	float2 f012 = Divide(f12 - f01, z[2] - z[0]);
	float2 polynomial[3];
	polynomial[0] = f012;
	polynomial[1] = polynomial[0];
	polynomial[0] = f01 - Multiply(polynomial[0], z[1]);
	polynomial[2] = polynomial[1];
	polynomial[1] = polynomial[0] - Multiply(polynomial[1], z[0]);
	polynomial[0] = f0 - Multiply(polynomial[0], z[0]);
	float weight_sum = 0.0f;
	weight_sum += RealPart(Multiply(b[0], polynomial[0]));
	weight_sum += RealPart(Multiply(b[1], polynomial[1]));
	weight_sum += RealPart(Multiply(b[2], polynomial[2]));
	// Turn the normalized absorbance into transmittance
	return exp(-b_0 * weight_sum);
}

/*! This function reconstructs the transmittance at the given depth from three
	normalized trigonometric moments. */
float ComputeTransmittanceTrigonometric(float b_0, float2 trig_b[3], float depth, float bias, float overestimation, float4 wrapping_zone_parameters)
{
	// Apply biasing and reformat the inputs a little bit
	float moment_scale = 1.0f - bias;
	float2 b[4] = {
		float2(1.0f, 0.0f),
		trig_b[0] * moment_scale,
		trig_b[1] * moment_scale,
		trig_b[2] * moment_scale
	};
	// Compute a Cholesky factorization of the Toeplitz matrix
	float D00 = RealPart(b[0]);
	float InvD00 = 1.0f / D00;
	float2 L10 = (b[1])*InvD00;
	float D11 = RealPart(b[0] - D00 * Multiply(L10, Conjugate(L10)));
	float InvD11 = 1.0f / D11;
	float2 L20 = (b[2])*InvD00;
	float2 L21 = (b[1] - D00 * Multiply(L20, Conjugate(L10)))*InvD11;
	float D22 = RealPart(b[0] - D00 * Multiply(L20, Conjugate(L20)) - D11 * Multiply(L21, Conjugate(L21)));
	float InvD22 = 1.0f / D22;
	float2 L30 = (b[3])*InvD00;
	float2 L31 = (b[2] - D00 * Multiply(L30, Conjugate(L10)))*InvD11;
	float2 L32 = (b[1] - D00 * Multiply(L30, Conjugate(L20)) - D11 * Multiply(L31, Conjugate(L21)))*InvD22;
	float D33 = RealPart(b[0] - D00 * Multiply(L30, Conjugate(L30)) - D11 * Multiply(L31, Conjugate(L31)) - D22 * Multiply(L32, Conjugate(L32)));
	float InvD33 = 1.0f / D33;
	// Solve a linear system to get the relevant polynomial
	float phase = mad(depth, wrapping_zone_parameters.y, wrapping_zone_parameters.y);
	float2 circle_point;
	sincos(phase, circle_point.y, circle_point.x);
	float2 circle_point_pow2 = Multiply(circle_point, circle_point);
	float2 c[4] = {
		float2(1.0f,0.0f),
		circle_point,
		circle_point_pow2,
		Multiply(circle_point, circle_point_pow2)
	};
	c[1] -= Multiply(L10, c[0]);
	c[2] -= Multiply(L20, c[0]) + Multiply(L21, c[1]);
	c[3] -= Multiply(L30, c[0]) + Multiply(L31, c[1]) + Multiply(L32, c[2]);
	c[0] *= InvD00;
	c[1] *= InvD11;
	c[2] *= InvD22;
	c[3] *= InvD33;
	c[2] -= Multiply(Conjugate(L32), c[3]);
	c[1] -= Multiply(Conjugate(L21), c[2]) + Multiply(Conjugate(L31), c[3]);
	c[0] -= Multiply(Conjugate(L10), c[1]) + Multiply(Conjugate(L20), c[2]) + Multiply(Conjugate(L30), c[3]);
	// Compute roots of the polynomial
	float2 pRoot[3];
	SolveCubicBlinn(pRoot, Conjugate(c[3]), Conjugate(c[2]), Conjugate(c[1]), Conjugate(c[0]));
	// The roots are known to be normalized but for reasons of numerical 
	// stability it can be better to enforce that
	//pRoot[0]=normalize(pRoot[0]);
	//pRoot[1]=normalize(pRoot[1]);
	//pRoot[2]=normalize(pRoot[2]);
	// Figure out how to weight the weights
	float depth_parameter = circleToParameter(circle_point);
	float4 weight_factor;
	weight_factor[0] = overestimation;
	[unroll]
	for (int i = 0; i != 3; ++i)
	{
		float root_parameter = circleToParameter(pRoot[i]);
		weight_factor[i + 1] = getRootWeightFactor(depth_parameter, root_parameter, wrapping_zone_parameters);
	}
	// Compute the appropriate linear combination of weights
	float2 z[4] = { circle_point, pRoot[0], pRoot[1], pRoot[2] };
	float f0 = weight_factor[0];
	float f1 = weight_factor[1];
	float f2 = weight_factor[2];
	float f3 = weight_factor[3];
	float2 f01 = Divide(f1 - f0, z[1] - z[0]);
	float2 f12 = Divide(f2 - f1, z[2] - z[1]);
	float2 f23 = Divide(f3 - f2, z[3] - z[2]);
	float2 f012 = Divide(f12 - f01, z[2] - z[0]);
	float2 f123 = Divide(f23 - f12, z[3] - z[1]);
	float2 f0123 = Divide(f123 - f012, z[3] - z[0]);
	float2 polynomial[4];
	polynomial[0] = f0123;
	polynomial[1] = polynomial[0];
	polynomial[0] = f012 - Multiply(polynomial[0], z[2]);
	polynomial[2] = polynomial[1];
	polynomial[1] = polynomial[0] - Multiply(polynomial[1], z[1]);
	polynomial[0] = f01 - Multiply(polynomial[0], z[1]);
	polynomial[3] = polynomial[2];
	polynomial[2] = polynomial[1] - Multiply(polynomial[2], z[0]);
	polynomial[1] = polynomial[0] - Multiply(polynomial[1], z[0]);
	polynomial[0] = f0 - Multiply(polynomial[0], z[0]);
	float weight_sum = 0;
	weight_sum += RealPart(Multiply(b[0], polynomial[0]));
	weight_sum += RealPart(Multiply(b[1], polynomial[1]));
	weight_sum += RealPart(Multiply(b[2], polynomial[2]));
	weight_sum += RealPart(Multiply(b[3], polynomial[3]));
	// Turn the normalized absorbance into transmittance
	return exp(-b_0 * weight_sum);
}

/*! This function reconstructs the transmittance at the given depth from four
	normalized trigonometric moments.*/
float ComputeTransmittanceTrigonometric(float b_0, float2 trig_b[4], float depth, float bias, float overestimation, float4 wrapping_zone_parameters)
{
	// Apply biasing and reformat the inputs a little bit
	float moment_scale = 1.0f - bias;
	float2 b[5] = {
		float2(1.0f, 0.0f),
		trig_b[0] * moment_scale,
		trig_b[1] * moment_scale,
		trig_b[2] * moment_scale,
		trig_b[3] * moment_scale
	};
	// Compute a Cholesky factorization of the Toeplitz matrix
	float D00 = RealPart(b[0]);
	float InvD00 = 1.0 / D00;
	float2 L10 = (b[1])*InvD00;
	float D11 = RealPart(b[0] - D00 * Multiply(L10, Conjugate(L10)));
	float InvD11 = 1.0 / D11;
	float2 L20 = (b[2])*InvD00;
	float2 L21 = (b[1] - D00 * Multiply(L20, Conjugate(L10)))*InvD11;
	float D22 = RealPart(b[0] - D00 * Multiply(L20, Conjugate(L20)) - D11 * Multiply(L21, Conjugate(L21)));
	float InvD22 = 1.0 / D22;
	float2 L30 = (b[3])*InvD00;
	float2 L31 = (b[2] - D00 * Multiply(L30, Conjugate(L10)))*InvD11;
	float2 L32 = (b[1] - D00 * Multiply(L30, Conjugate(L20)) - D11 * Multiply(L31, Conjugate(L21)))*InvD22;
	float D33 = RealPart(b[0] - D00 * Multiply(L30, Conjugate(L30)) - D11 * Multiply(L31, Conjugate(L31)) - D22 * Multiply(L32, Conjugate(L32)));
	float InvD33 = 1.0 / D33;
	float2 L40 = (b[4])*InvD00;
	float2 L41 = (b[3] - D00 * Multiply(L40, Conjugate(L10)))*InvD11;
	float2 L42 = (b[2] - D00 * Multiply(L40, Conjugate(L20)) - D11 * Multiply(L41, Conjugate(L21)))*InvD22;
	float2 L43 = (b[1] - D00 * Multiply(L40, Conjugate(L30)) - D11 * Multiply(L41, Conjugate(L31)) - D22 * Multiply(L42, Conjugate(L32)))*InvD33;
	float D44 = RealPart(b[0] - D00 * Multiply(L40, Conjugate(L40)) - D11 * Multiply(L41, Conjugate(L41)) - D22 * Multiply(L42, Conjugate(L42)) - D33 * Multiply(L43, Conjugate(L43)));
	float InvD44 = 1.0 / D44;
	// Solve a linear system to get the relevant polynomial
	float phase = mad(depth, wrapping_zone_parameters.y, wrapping_zone_parameters.y);
	float2 circle_point;
	sincos(phase, circle_point.y, circle_point.x);
	float2 circle_point_pow2 = Multiply(circle_point, circle_point);
	float2 c[5] = {
		float2(1.0f,0.0f),
		circle_point,
		circle_point_pow2,
		Multiply(circle_point, circle_point_pow2),
		Multiply(circle_point_pow2, circle_point_pow2)
	};
	c[1] -= Multiply(L10, c[0]);
	c[2] -= Multiply(L20, c[0]) + Multiply(L21, c[1]);
	c[3] -= Multiply(L30, c[0]) + Multiply(L31, c[1]) + Multiply(L32, c[2]);
	c[4] -= Multiply(L40, c[0]) + Multiply(L41, c[1]) + Multiply(L42, c[2]) + Multiply(L43, c[3]);
	c[0] *= InvD00;
	c[1] *= InvD11;
	c[2] *= InvD22;
	c[3] *= InvD33;
	c[4] *= InvD44;
	c[3] -= Multiply(Conjugate(L43), c[4]);
	c[2] -= Multiply(Conjugate(L32), c[3]) + Multiply(Conjugate(L42), c[4]);
	c[1] -= Multiply(Conjugate(L21), c[2]) + Multiply(Conjugate(L31), c[3]) + Multiply(Conjugate(L41), c[4]);
	c[0] -= Multiply(Conjugate(L10), c[1]) + Multiply(Conjugate(L20), c[2]) + Multiply(Conjugate(L30), c[3]) + Multiply(Conjugate(L40), c[4]);
	// Compute roots of the polynomial
	float2 pRoot[4];
	SolveQuarticNeumark(pRoot, Conjugate(c[4]), Conjugate(c[3]), Conjugate(c[2]), Conjugate(c[1]), Conjugate(c[0]));
	// Figure out how to weight the weights
	float depth_parameter = circleToParameter(circle_point);
	float weight_factor[5];
	weight_factor[0] = overestimation;
	[unroll]
	for (int i = 0; i != 4; ++i)
	{
		float root_parameter = circleToParameter(pRoot[i]);
		weight_factor[i + 1] = getRootWeightFactor(depth_parameter, root_parameter, wrapping_zone_parameters);
	}
	// Compute the appropriate linear combination of weights
	float2 z[5] = { circle_point, pRoot[0], pRoot[1], pRoot[2], pRoot[3] };
	float f0 = weight_factor[0];
	float f1 = weight_factor[1];
	float f2 = weight_factor[2];
	float f3 = weight_factor[3];
	float f4 = weight_factor[4];
	float2 f01 = Divide(f1 - f0, z[1] - z[0]);
	float2 f12 = Divide(f2 - f1, z[2] - z[1]);
	float2 f23 = Divide(f3 - f2, z[3] - z[2]);
	float2 f34 = Divide(f4 - f3, z[4] - z[3]);
	float2 f012 = Divide(f12 - f01, z[2] - z[0]);
	float2 f123 = Divide(f23 - f12, z[3] - z[1]);
	float2 f234 = Divide(f34 - f23, z[4] - z[2]);
	float2 f0123 = Divide(f123 - f012, z[3] - z[0]);
	float2 f1234 = Divide(f234 - f123, z[4] - z[1]);
	float2 f01234 = Divide(f1234 - f0123, z[4] - z[0]);
	float2 polynomial[5];
	polynomial[0] = f01234;
	polynomial[1] = polynomial[0];
	polynomial[0] = f0123 - Multiply(polynomial[0], z[3]);
	polynomial[2] = polynomial[1];
	polynomial[1] = polynomial[0] - Multiply(polynomial[1], z[2]);
	polynomial[0] = f012 - Multiply(polynomial[0], z[2]);
	polynomial[3] = polynomial[2];
	polynomial[2] = polynomial[1] - Multiply(polynomial[2], z[1]);
	polynomial[1] = polynomial[0] - Multiply(polynomial[1], z[1]);
	polynomial[0] = f01 - Multiply(polynomial[0], z[1]);
	polynomial[4] = polynomial[3];
	polynomial[3] = polynomial[2] - Multiply(polynomial[3], z[0]);
	polynomial[2] = polynomial[1] - Multiply(polynomial[2], z[0]);
	polynomial[1] = polynomial[0] - Multiply(polynomial[1], z[0]);
	polynomial[0] = f0 - Multiply(polynomial[0], z[0]);
	float weight_sum = 0;
	weight_sum += RealPart(Multiply(b[0], polynomial[0]));
	weight_sum += RealPart(Multiply(b[1], polynomial[1]));
	weight_sum += RealPart(Multiply(b[2], polynomial[2]));
	weight_sum += RealPart(Multiply(b[3], polynomial[3]));
	weight_sum += RealPart(Multiply(b[4], polynomial[4]));
	// Turn the normalized absorbance into transmittance
	return exp(-b_0 * weight_sum);
}
#endif
