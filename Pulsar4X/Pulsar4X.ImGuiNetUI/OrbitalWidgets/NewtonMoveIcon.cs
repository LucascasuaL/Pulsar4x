using System;
using System.Collections.Generic;
using Pulsar4X.ECSLib;
using SDL2;

namespace Pulsar4X.SDL2UI
{
    
    /// <summary>
    /// The key parts of this are taken from the paper
    /// "Drawing ellipses, hyperbolas or parabolas with a
    ///  fixed number of points and maximum inscribed area"
    /// by L.R. Smith
    /// </summary>
    public class NewtonMoveIcon : Icon
    {
        protected EntityManager _mgr;
        NewtonMoveDB _newtonMoveDB;
        PositionDB parentPosDB;
        PositionDB myPosDB;
        double _sgp;
        private double _sgpAU;
        int _index = 0;
        int _numberOfPoints;
        //internal float a;
        //protected float b;
        protected PointD[] _points; //we calculate points around the ellipse and add them here. when we draw them we translate all the points. 
        protected SDL.SDL_Point[] _drawPoints = new SDL.SDL_Point[0];
        PointD[] _debugPoints;
        SDL.SDL_Point[] _debugDrawPoints = new SDL.SDL_Point[0];

        //user adjustable variables:
        internal UserOrbitSettings.OrbitBodyType BodyType = UserOrbitSettings.OrbitBodyType.Unknown;
        internal UserOrbitSettings.OrbitTrajectoryType TrajectoryType = UserOrbitSettings.OrbitTrajectoryType.Unknown;
        protected List<List<UserOrbitSettings>> _userOrbitSettingsMtx;
        protected UserOrbitSettings _userSettings { get { return _userOrbitSettingsMtx[(int)BodyType][(int)TrajectoryType]; } }

        //change after user makes adjustments:
        protected byte _numberOfArcSegments = 255; //how many segments in a complete 360 degree ellipse. this is set in UserOrbitSettings, localy adjusted because the whole point array needs re-creating when it changes. 
        protected int _numberOfDrawSegments; //this is now many segments get drawn in the ellipse, ie if the _ellipseSweepAngle or _numberOfArcSegments are less, less will be drawn.
        protected float _segmentArcSweepRadians; //how large each segment in the drawn portion of the ellipse.  
        protected float _alphaChangeAmount;

        private double _dv = 0;
        private KeplerElements _ke;
        
        
        
        public NewtonMoveIcon(EntityState entityState, List<List<UserOrbitSettings>> settings) : base(entityState.Entity.GetDataBlob<NewtonMoveDB>().SOIParent.GetDataBlob<PositionDB>())
        {
            BodyType = entityState.BodyType;
            TrajectoryType = UserOrbitSettings.OrbitTrajectoryType.Hyperbolic;
            _mgr = entityState.Entity.Manager;
            _newtonMoveDB = entityState.Entity.GetDataBlob<NewtonMoveDB>();
            parentPosDB = _newtonMoveDB.SOIParent.GetDataBlob<PositionDB>();
            _positionDB = parentPosDB;
            myPosDB = entityState.Entity.GetDataBlob<PositionDB>();
            _userOrbitSettingsMtx = settings;
            var parentMass = entityState.Entity.GetDataBlob<NewtonMoveDB>().ParentMass;
            var myMass = entityState.Entity.GetDataBlob<MassVolumeDB>().MassDry;
            var _sgp1 = GameConstants.Science.GravitationalConstant * (parentMass + myMass) / 3.347928976e33;

            _sgp = OrbitMath.CalculateStandardGravityParameterInM3S2(myMass, parentMass);
            _sgpAU = GMath.GrabitiationalParameter_Au3s2(parentMass + myMass);
            _ke = _newtonMoveDB.GetElements();
            
            
            UpdateUserSettings();
            //CreatePointArray();
            OnPhysicsUpdate();
        }
        public void UpdateUserSettings()
        {
            
            //if this is the case, we need to rebuild the whole set of points. 
            if (_userSettings.NumberOfArcSegments != _numberOfArcSegments)
            {
                _numberOfArcSegments = _userSettings.NumberOfArcSegments;
                _segmentArcSweepRadians = (float)(Math.PI * 2.0 / _numberOfArcSegments);
                _numberOfDrawSegments = (int)Math.Max(1, (_userSettings.EllipseSweepRadians / _segmentArcSweepRadians));
                _alphaChangeAmount = ((float)_userSettings.MaxAlpha - _userSettings.MinAlpha) / _numberOfDrawSegments;
                _numberOfPoints = _numberOfDrawSegments + 1;
                CreatePointArray();
            }
            _segmentArcSweepRadians = (float)(Math.PI * 2.0 / _numberOfArcSegments);
            _numberOfDrawSegments = (int)Math.Max(1, (_userSettings.EllipseSweepRadians / _segmentArcSweepRadians));
            _alphaChangeAmount = ((float)_userSettings.MaxAlpha - _userSettings.MinAlpha) / _numberOfDrawSegments;
            _numberOfPoints = _numberOfDrawSegments + 1;
        }



        internal void CreatePointArray()
        {
            if(_ke.Eccentricity < 1)
            {
                TrajectoryType = UserOrbitSettings.OrbitTrajectoryType.Elliptical;
                CreateEllipsePoints();
            }
            else
            {
                TrajectoryType = UserOrbitSettings.OrbitTrajectoryType.Hyperbolic;
                CreateHyperbolicPoints();
            }
        }

        private void CreateHyperbolicPoints()
        {
            Vector3 vel = _newtonMoveDB.CurrentVector_ms;
            Vector3 pos = myPosDB.RelativePosition_m;
            //Vector3 eccentVector = OrbitMath.EccentricityVector(_sgp, pos, vel);
            Vector3 eccentVector = OrbitMath.EccentricityVector(_sgp, pos, vel);
            double e1 = eccentVector.Length();
            
            double e = _ke.Eccentricity; 
            double r = pos.Length();
            double v = vel.Length();
            double a = _ke.SemiMajorAxis;    //semiMajor Axis
            double b = _ke.SemiMinorAxis;     //semiMinor Axis

            double a1 = 1 / (2 / r - Math.Pow(v, 2) / _sgp);    //semiMajor Axis
            double b1 = -a * Math.Sqrt(Math.Pow(e, 2) - 1);     //semiMinor Axis
            

            double linierEccentricity = e * a;
            double soi = OrbitProcessor.GetSOI_m(_newtonMoveDB.SOIParent);

            //longditudeOfPeriapsis;
            double _lop = _ke.AoP + _ke.LoAN;

            double p = EllipseMath.SemiLatusRectum(a, e);
            double angleToSOIPoint = Math.Abs(OrbitMath.AngleAtRadus(soi, p, e));
            double thetaMax = angleToSOIPoint;// - _lop;
/*
            double maxX = soi * Math.Cos(angleToSOIPoint);
            maxX = maxX - a + linierEccentricity;
            double barA = maxX / a;
            double barB = barA * barA - 1;
            double barC = Math.Sqrt(barB);
            double thetaMax = Math.Log(barA + barC);
*/
            //first we calculate the position, 

            if (_numberOfPoints % 2 == 0)
                _numberOfPoints += 1;
            int ctrIndex = _numberOfPoints / 2;
            double dtheta = thetaMax / (ctrIndex - 1);
            double fooA = Math.Cosh(dtheta);
            double fooB = (a / b) * Math.Sinh(dtheta);
            double fooC = (b / a) * Math.Sinh(dtheta);
            double xn = a;
            double yn = 0;

            var points = new PointD[ctrIndex + 1];
            points[0] = new PointD() { X = xn, Y = yn };
            for (int i = 1; i < ctrIndex + 1; i++)
            {
                var lastx = xn;
                var lasty = yn;
                xn = fooA * lastx + fooB * lasty;
                yn = fooC * lastx + fooA * lasty;
                points[i] = new PointD() { X = xn, Y = yn };
            }


            _points = new PointD[_numberOfPoints];


            for (int i = 0; i < points.Length ; i++)
            {
                _points[i] = new PointD()
                {
                    X = -points[i].X,
                    Y = -points[i].Y,
                };
                _points[i + ctrIndex] = new PointD()
                {
                    X = points[i].X,
                    Y = points[i].Y,
                };
            }
            
            /*
            _points[ctrIndex] = new PointD()
            {
                
                X = ((points[0].X - linierEccentricity )* Math.Cos(_lop)) - (points[0].Y * Math.Sin(_lop)),
                Y = ((points[0].X - linierEccentricity) * Math.Sin(_lop)) + (points[0].Y * Math.Cos(_lop))
            };
            for (int i = 1; i < ctrIndex + 1; i++)
            {
                double x = points[i].X - linierEccentricity; //adjust for the focal point
                double ya = points[i].Y;
                double yb = -points[i].Y;
                double x2a = (x * Math.Cos(_lop)) - (ya * Math.Sin(_lop)); //rotate to loan
                double y2a = (x * Math.Sin(_lop)) + (ya * Math.Cos(_lop));
                double x2b = (x * Math.Cos(_lop)) - (yb * Math.Sin(_lop));
                double y2b = (x * Math.Sin(_lop)) + (yb * Math.Cos(_lop));
                _points[ctrIndex + i] = new PointD()
                {
                    X = x2a,
                    Y = y2a 
                };

                _points[ctrIndex - i] = new PointD()
                {
                    X = x2b,
                    Y = y2b
                };
            }
            */
        }

        private void CreateEllipsePoints()
        {

            double a = _ke.SemiMajorAxis;
            double b = _ke.SemiMinorAxis;
            double linierEccentricity = _ke.Eccentricity * a;
            double _lop = _ke.AoP + _ke.LoAN;            
            
            double dTheta = 2 * Math.PI / (_numberOfPoints - 1);
            
            double ct = Math.Cos(_lop);
            double st = Math.Sign(_lop);
            double cdp = Math.Cos(dTheta);
            double sdp = Math.Sin(dTheta);
            double fooA = cdp + sdp * st * ct * (a / b - b / a);
            double fooB = -sdp * ((b * st) * (b * st) + ((a * ct) * (a * ct))) / (a * b);
            double fooC = sdp * ((b * st) * (b * st) + ((a * ct) * (a * ct))) / (a * b);
            double fooD = cdp + sdp * st * ct * (b / a - a / b);
            fooD -= (fooC * fooB) / fooA;
            fooC = fooC / fooA;

            double x = a * ct;
            double y = a * st;

            //this is the offset ie the distance between focal and center.
            double xc1 = a *  Math.Sin(_lop) - linierEccentricity; //we add the focal distance so the focal point is "center"
            double yc1 = b * Math.Cos(_lop);
            var coslop = 1 * Math.Cos(_lop);
            var sinlop = 1 * Math.Sin(_lop);
            //and then rotate it to the longditude of periapsis.
            double xc = (xc1 * coslop) - (yc1 * sinlop);
            double yc = (xc1 * sinlop) + (yc1 * coslop);

            
            _points = new PointD[_numberOfPoints];

            for (int i = 0; i < _numberOfPoints; i++)
            {
                _points[i] = new PointD()
                {
                    X = xc + x,
                    Y = yc + y,
                };
                x = fooA * x + fooB * y;
                y = fooC * x + fooD * y;
            }
            

        }
        
        public override void OnFrameUpdate(Matrix matrix, Camera camera)
        {

            //translate to position
            //resize from m to au
            //resize for zoom
            var screenPos = camera.ViewCoordinate_m(WorldPosition_m); //focal point 
            var translate = Matrix3d.IDTranslate(screenPos.x, screenPos.y, 1);
            var scaleToAu = Matrix3d.IDScale(6.6859E-12, 6.6859E-12, 6.6859E-12);
            var scaleToZoom = Matrix3d.IDScale(camera.ZoomLevel, camera.ZoomLevel, camera.ZoomLevel);
            var mtx = scaleToAu * scaleToZoom * translate; 
            _drawPoints = new SDL.SDL_Point[_numberOfDrawSegments];
            for (int i = 0; i < _numberOfDrawSegments; i++)
            {
                var point = mtx.Transform(new Vector3(_points[i].X, _points[i].Y, 1));
                int x = (int)Math.Round(point.X);
                int y = (int)Math.Round(point.Y);
                _drawPoints[i] = new SDL.SDL_Point() { x = x, y = y };
            }
            
            /*
            var foo = camera.ViewCoordinate_m(WorldPosition_m);
            var vsp = new PointD
            {
                X = foo.x,
                Y = foo.y
            };


            int index = _index;
            _drawPoints = new SDL.SDL_Point[_numberOfDrawSegments];
            var _bodyRalitivePos = _positionDB.RelativePosition_AU;
            //first index in the drawPoints is the position of the body
            var translated = matrix.TransformD(_bodyRalitivePos.X, _bodyRalitivePos.Y);
            _drawPoints[0] = new SDL.SDL_Point() { x = (int)(vsp.X + translated.X), y = (int)(vsp.Y + translated.Y) };
            
            for (int i = 1; i < _numberOfDrawSegments; i++)
            {
                if (index < _numberOfArcSegments - 1)

                    index++;
                else
                    index = 0;

                translated = matrix.TransformD(_points[index].X, _points[index].Y); //add zoom transformation. 

                int x = (int)(vsp.X + translated.X);
                int y = (int)(vsp.Y + translated.Y);

                _drawPoints[i] = new SDL.SDL_Point() { x = x, y = y };
            }
            */
        }
        
        public override void Draw(IntPtr rendererPtr, Camera camera)
        {
            //now we draw a line between each of the points in the translatedPoints[] array.
            if (_drawPoints.Length < _numberOfDrawSegments - 1)
                return;
            float alpha = _userSettings.MaxAlpha;
            for (int i = 0; i < _numberOfDrawSegments - 1; i++)
            {
                SDL.SDL_SetRenderDrawColor(rendererPtr, _userSettings.Red, _userSettings.Grn, _userSettings.Blu, (byte)alpha);//we cast the alpha here to stop rounding errors creaping up. 
                SDL.SDL_RenderDrawLine(rendererPtr, _drawPoints[i].x, _drawPoints[i].y, _drawPoints[i + 1].x, _drawPoints[i +1].y);
                alpha -= _alphaChangeAmount; 
            }
        }


    }
}