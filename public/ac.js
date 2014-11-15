'use strict';

var AirControl = React.createClass({
  getInitialState: function () {
    return {
      loggedIn: false
    , loading: false
    , temp: 60
    , on: 0
    , mode: 0
    , fanspeed: 0
    };
  }

, render: function() {
    if (!this.state.loggedIn) return null;
    if (this.state.loading) {
      return (
        <div className='row'>
          <div className='col-xs-12'>
            <i className='fa fa-spinner fa-spin'></i> Loading
          </div>
        </div>
      );
    }

    var modeClass = [];
    for (var i=0; i < 4; i++) {
      if (this.state.mode === i) modeClass.push('btn btn-primary');
      else modeClass.push('btn btn-default');
    }

    var speedClass = [];
    for (var i=0; i < 3; i++) {
      if (this.state.fanspeed === i) speedClass.push('btn btn-primary');
      else speedClass.push('btn btn-default');
    }

    return (<div>
      <div className='row'>
        <div className='col-xs-6'><button type='button' onClick={this.setPower.bind(this, 1)} className='btn btn-success' disabled={this.state.on === 1}><i className='fa fa-power-off'></i> ON</button></div>
        <div className='col-xs-6'><button type='button' onClick={this.setPower.bind(this, 0)} className='btn btn-danger' disabled={this.state.on === 0}><i className='fa fa-power-off'></i> OFF</button></div>
      </div>
      <div className='row'>
        <div className='col-xs-2'>{this.state.temp}&deg;</div>
        <div className='col-xs-10'>
          <input type='range' id='temp' min='60' max='86' value={this.state.temp} step='1' onChange={this.changeTemp}></input>
        </div>
      </div>
      <div className='row'>
        <div className='col-xs-3'><button type='button' onClick={this.changeMode.bind(this, 0)} className={modeClass[0]}><i className='fa fa-cog'></i> Cool</button></div>
        <div className='col-xs-3'><button type='button' onClick={this.changeMode.bind(this, 1)} className={modeClass[1]}><i className='fa fa-dollar'></i> Saver</button></div>
        <div className='col-xs-3'><button type='button' onClick={this.changeMode.bind(this, 2)} className={modeClass[2]}><i className='fa fa-flag'></i> Fan</button></div>
        <div className='col-xs-3'><button type='button' onClick={this.changeMode.bind(this, 3)} className={modeClass[3]}><i className='fa fa-fire'></i> Dry</button></div>
      </div>
      <div className='row'>
        <div className='col-xs-4'><button type='button' onClick={this.changeSpeed.bind(this, 0)} className={speedClass[0]}><i className='fa fa-chevron-right'></i> 1</button></div>
        <div className='col-xs-4'><button type='button' onClick={this.changeSpeed.bind(this, 1)} className={speedClass[1]}><i className='fa fa-chevron-right'></i><i className='fa fa-chevron-right'></i> 2</button></div>
        <div className='col-xs-4'><button type='button' onClick={this.changeSpeed.bind(this, 2)} className={speedClass[2]}><i className='fa fa-chevron-right'></i><i className='fa fa-chevron-right'></i><i className='fa fa-chevron-right'></i> 3</button></div>
      </div>
    </div>);
  }

, componentDidMount: function() {
    sparkLogin(function (data) {
      document.getElementById('spark-login-button').style.display = 'none';
      if (this.isMounted()) {
        this.token = data;
        this.setState({ loading: true, loggedIn: true });
        this.load();
      }
    }.bind(this));
  }

, load: function() {
    spark.listDevices(function (err, devices) {
      if (err || !devices.length) return;
      this.device = devices[0];

      this.loadVariable('on');
      this.loadVariable('temp');
      this.loadVariable('mode');
      this.loadVariable('fanspeed');
    }.bind(this));
  }

, loadVariable: function(name) {
    this.device.getVariable(name, function (err, data) {
      if (err) return;
      var obj = { loading: false };
      obj[name] = data.result;
      this.setState(obj);
    }.bind(this));
  }

, changeTemp: function(e) {
    this.setState({ temp: e.target.value });
    this.setTemp(e.target.value);
  }

, setTemp: _.debounce(function(value) {
    this.device.callFunction('setTemp', '' + value, function (err, data) {
      if (err) {
        console.error(err);
        return;
      }
    }.bind(this));
  }, 500)

, changeMode: function(mode) {
    var prevMode = this.state.mode;
    this.setState({ mode: mode });
    this.device.callFunction('changeMode', '' + mode, function (err, data) {
      if (err) {
        this.setState({ mode: prevMode });
        return;
      }
    }.bind(this));
  }

, changeSpeed: function(speed) {
    var prevSpeed = this.state.fanspeed;
    this.setState({ fanspeed: speed });
    this.device.callFunction('changeSpeed', '' + speed, function (err, data) {
      if (err) {
        this.setState({ fanspeed: prevSpeed });
        return;
      }
    }.bind(this));
  }

, setPower: function(power) {
    var prevPower = this.state.on;
    this.setState({ on: power });
    this.device.callFunction('power', power ? 'ON' : 'OFF', function (err, data) {
      if (err) {
        this.setState({ on: prevPower });
        return;
      }
    }.bind(this));
  }
});

React.render(<AirControl />, document.getElementById('controls'));
